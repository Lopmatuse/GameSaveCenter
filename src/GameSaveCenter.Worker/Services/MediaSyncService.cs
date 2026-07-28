using System.Security.Cryptography;
using System.Text.Json;
using GameSaveCenter.Contracts;
using GameSaveCenter.Core.Services;
using GameSaveCenter.Worker.Configuration;
using GameSaveCenter.Worker.Infrastructure;
using GameSaveCenter.Worker.Persistence;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Services;

/// <summary>
/// Incrementally copies screenshots and clips into a stable archive. Files are deduplicated
/// by SHA-256 and source deletion never removes the archive copy. Shared capture folders are
/// scanned once and ambiguous items are preserved in a manual classification inbox.
/// </summary>
public sealed class MediaSyncService
{
    private static readonly HashSet<string> ImageExtensions=new(StringComparer.OrdinalIgnoreCase){".png",".jpg",".jpeg",".webp",".bmp"};
    private static readonly HashSet<string> VideoExtensions=new(StringComparer.OrdinalIgnoreCase){".mp4",".mkv",".mov",".webm",".avi"};
    private readonly WorkerOptions _options;
    private readonly GameCatalogService _catalog;
    private readonly SqliteStateStore _store;
    private readonly RcloneClient _rclone;
    private readonly TaskCoordinator _tasks;
    private readonly ILogger<MediaSyncService> _logger;

    public MediaSyncService(WorkerOptions options,GameCatalogService catalog,SqliteStateStore store,RcloneClient rclone,TaskCoordinator tasks,ILogger<MediaSyncService> logger)
    { _options=options;_catalog=catalog;_store=store;_rclone=rclone;_tasks=tasks;_logger=logger; }

    public async Task<List<TaskStatusDto>> SyncAsync(MediaSyncRequestDto request,CancellationToken token)
    {
        if(!_options.EnableMediaSync) return new List<TaskStatusDto>();
        var allGames=await _catalog.GetGamesAsync(token).ConfigureAwait(false);
        var selectedGames=allGames;
        if(request.PlayniteIds.Count>0)
            selectedGames=allGames.Where(x=>request.PlayniteIds.Contains(x.PlayniteId,StringComparer.OrdinalIgnoreCase)).ToList();

        var output=new List<TaskStatusDto>();
        if(!request.SharedOnly)
            foreach(var game in selectedGames)
                output.Add(await SyncGameSourcesAsync(game,request,token).ConfigureAwait(false));

        if(request.IncludeUnassignedInbox)
            output.Add(await SyncSharedSourcesAsync(allGames,request,token).ConfigureAwait(false));
        return output;
    }

    /// <summary>Moves either an inbox item or an already classified item to the selected game archive.</summary>
    public async Task<MediaItemDto> ReassignAsync(ReassignMediaRequestDto request,CancellationToken token)
    {
        if(string.IsNullOrWhiteSpace(request.MediaId)||string.IsNullOrWhiteSpace(request.TargetPlayniteId))
            throw new InvalidOperationException("Media and target game are required.");
        var item=await _store.GetMediaByIdAsync(request.MediaId,token).ConfigureAwait(false)
                 ??throw new InvalidOperationException("媒体记录不存在或已经被清理。");
        var game=await _store.GetGameAsync(request.TargetPlayniteId,token).ConfigureAwait(false)
                 ??throw new InvalidOperationException("目标游戏不存在于当前 Playnite 游戏库。");
        var extension=Path.GetExtension(File.Exists(item.ArchivePath)?item.ArchivePath:item.OriginalPath);
        var destination=BuildArchivePath(game,item.Source,item.Kind,item.CapturedUtc,item.Sha256,extension);
        await RelocateArchivedCopyAsync(item,destination,token).ConfigureAwait(false);
        await _store.AssignMediaAsync(item.MediaId,game.PlayniteId,destination,token).ConfigureAwait(false);
        await _store.AppendAuditAsync("Media","媒体已人工归类",JsonSerializer.Serialize(new{item.MediaId,game.PlayniteId,game.Name,item.OriginalPath,destination}),token).ConfigureAwait(false);
        item.PlayniteId=game.PlayniteId;
        item.ArchivePath=destination;
        item.ClassificationState="Assigned";
        item.ClassificationReason=string.Empty;
        item.CloudState="Pending";
        return item;
    }

    /// <summary>Removes an item from the inbox while retaining a recoverable local copy.</summary>
    public async Task<MediaItemDto> IgnoreAsync(IgnoreMediaRequestDto request,CancellationToken token)
    {
        if(string.IsNullOrWhiteSpace(request.MediaId))throw new InvalidOperationException("Media is required.");
        var item=await _store.GetMediaByIdAsync(request.MediaId,token).ConfigureAwait(false)
                 ??throw new InvalidOperationException("媒体记录不存在或已经被清理。");
        if(!string.Equals(item.ClassificationState,"Inbox",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("只有待归类收件箱中的媒体可以忽略。");
        var extension=Path.GetExtension(File.Exists(item.ArchivePath)?item.ArchivePath:item.OriginalPath);
        var destination=BuildIgnoredArchivePath(item,extension);
        await RelocateArchivedCopyAsync(item,destination,token).ConfigureAwait(false);
        await _store.IgnoreMediaAsync(item.MediaId,destination,token).ConfigureAwait(false);
        await _store.AppendAuditAsync("Media","媒体收件箱项目已忽略",JsonSerializer.Serialize(new{item.MediaId,item.OriginalPath,destination}),token).ConfigureAwait(false);
        item.ArchivePath=destination;
        item.ClassificationState="Ignored";
        item.ClassificationReason="用户已忽略";
        item.CloudState="NotApplicable";
        return item;
    }

    private Task<TaskStatusDto> SyncGameSourcesAsync(GameDescriptorDto game,MediaSyncRequestDto request,CancellationToken token)=>
        _tasks.RunAsync("MediaSync",game.PlayniteId,game.Name,async(progress,ct)=>
        {
            await progress.ReportAsync(5,"正在查找游戏专属媒体来源").ConfigureAwait(false);
            var sources=(await DiscoverGameSourcesAsync(game,ct).ConfigureAwait(false))
                .DistinctBy(x=>$"{x.Path}|{x.IncludePattern}",StringComparer.OrdinalIgnoreCase)
                .Where(x=>Directory.Exists(x.Path)).ToList();
            var candidates=EnumerateCandidates(sources);
            var copied=0;var index=0;
            foreach(var candidate in candidates.OrderBy(x=>x.Path,StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();index++;
                if(await ArchiveCandidateAsync(candidate.Path,candidate.Source,game,"游戏专属来源",ct).ConfigureAwait(false))copied++;
                if(index%20==0)await progress.ReportAsync(Math.Min(85,5+(int)(80d*index/Math.Max(1,candidates.Count))),$"已检查 {index}/{candidates.Count}").ConfigureAwait(false);
            }

            var policy=await _store.GetPolicyAsync(game.PlayniteId,ct).ConfigureAwait(false);
            if(_options.EnableCloudUpload&&(request.UploadAfterSync||policy.UploadAfterBackup)&&copied>0&&_rclone.IsConfigured)
            {
                await progress.ReportAsync(90,"正在复制媒体到云端").ConfigureAwait(false);
                var gameDirectory=Path.Combine(_options.MediaArchiveDirectory,Sanitize(game.Name));
                var remote=Path.Combine(Environment.MachineName,"Media",Sanitize(game.Name));
                var cloud=await _rclone.CopyAsync(gameDirectory,remote,ct).ConfigureAwait(false);
                if(!cloud.Success)
                {
                    await _store.UpdateMediaCloudStateAsync(game.PlayniteId,"Failed",ct).ConfigureAwait(false);
                    throw new InvalidOperationException("媒体已在本地归档，但云端复制失败："+cloud.StandardError);
                }
                await _store.UpdateMediaCloudStateAsync(game.PlayniteId,"Synced",ct).ConfigureAwait(false);
            }
            await progress.ReportAsync(100,$"媒体同步完成，新增 {copied} 个文件").ConfigureAwait(false);
        },token);

    private Task<TaskStatusDto> SyncSharedSourcesAsync(IReadOnlyList<GameDescriptorDto> games,MediaSyncRequestDto request,CancellationToken token)=>
        _tasks.RunAsync("MediaInbox",string.Empty,"公共媒体收件箱",async(progress,ct)=>
        {
            await progress.ReportAsync(5,"正在扫描公共截图与录像目录").ConfigureAwait(false);
            var sources=(await DiscoverSharedSourcesAsync(ct).ConfigureAwait(false))
                .DistinctBy(x=>$"{x.Path}|{x.IncludePattern}",StringComparer.OrdinalIgnoreCase)
                .Where(x=>Directory.Exists(x.Path)).ToList();
            var candidates=EnumerateCandidates(sources)
                .OrderByDescending(x=>SafeCapturedUtc(x.Path))
                .ToList();
            var session=await ResolveSharedSessionAsync(request.SessionId,ct).ConfigureAwait(false);
            var assigned=0;var inbox=0;var index=0;
            var assignedGameIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var candidate in candidates)
            {
                ct.ThrowIfCancellationRequested();index++;
                var resolution=ResolveSharedTarget(candidate.Path,games,session);
                var target=resolution.Game;
                if(await ArchiveCandidateAsync(candidate.Path,candidate.Source,target,resolution.Reason,ct).ConfigureAwait(false))
                {
                    if(target==null)inbox++;
                    else
                    {
                        assigned++;
                        assignedGameIds.Add(target.PlayniteId);
                    }
                }
                if(index%25==0)await progress.ReportAsync(Math.Min(90,5+(int)(85d*index/Math.Max(1,candidates.Count))),$"已检查 {index}/{candidates.Count}，待归类 {inbox}").ConfigureAwait(false);
                // Prevent one first run from filling the interface with thousands of ambiguous legacy captures.
                if(inbox>=200)
                {
                    _logger.LogInformation("Stopped shared media inbox import after the safety limit of 200 new unassigned items");
                    break;
                }
            }
            if(_options.EnableCloudUpload&&_rclone.IsConfigured&&assignedGameIds.Count>0)
            {
                foreach(var gameId in assignedGameIds)
                {
                    var game=games.First(x=>string.Equals(x.PlayniteId,gameId,StringComparison.OrdinalIgnoreCase));
                    var policy=await _store.GetPolicyAsync(gameId,ct).ConfigureAwait(false);
                    if(!request.UploadAfterSync&&!policy.UploadAfterBackup)continue;
                    await progress.ReportAsync(94,$"正在复制 {game.Name} 的公共媒体到云端").ConfigureAwait(false);
                    var gameDirectory=Path.Combine(_options.MediaArchiveDirectory,Sanitize(game.Name));
                    var remote=Path.Combine(Environment.MachineName,"Media",Sanitize(game.Name));
                    var cloud=await _rclone.CopyAsync(gameDirectory,remote,ct).ConfigureAwait(false);
                    if(!cloud.Success)
                    {
                        await _store.UpdateMediaCloudStateAsync(gameId,"Failed",ct).ConfigureAwait(false);
                        throw new InvalidOperationException($"{game.Name} 的公共媒体已在本地归档，但云端复制失败：{cloud.StandardError}");
                    }
                    await _store.UpdateMediaCloudStateAsync(gameId,"Synced",ct).ConfigureAwait(false);
                }
            }
            await progress.ReportAsync(100,$"公共媒体扫描完成，自动归类 {assigned} 个，待人工归类 {inbox} 个").ConfigureAwait(false);
        },token);

    private async Task<bool> ArchiveCandidateAsync(string path,MediaSourceKind source,GameDescriptorDto? game,string classificationReason,CancellationToken token)
    {
        try
        {
            if(!await IsStableAsync(path,token).ConfigureAwait(false))return false;
            var hash=await ComputeSha256Async(path,token).ConfigureAwait(false);
            if(await _store.MediaHashExistsAsync(hash,token).ConfigureAwait(false))return false;
            var info=new FileInfo(path);
            if(!info.Exists)return false;
            var captured=info.CreationTimeUtc==DateTime.MinValue?info.LastWriteTimeUtc:info.CreationTimeUtc;
            var kind=ImageExtensions.Contains(info.Extension)?MediaKind.Screenshot:MediaKind.VideoClip;
            var archive=game==null
                ?BuildInboxArchivePath(source,kind,captured,hash,info.Extension)
                :BuildArchivePath(game,source,kind,captured,hash,info.Extension);
            Directory.CreateDirectory(Path.GetDirectoryName(archive)!);
            await CopyAtomicallyAsync(path,archive,token).ConfigureAwait(false);
            await _store.AddMediaAsync(new MediaItemDto
            {
                MediaId=Guid.NewGuid().ToString("N"),PlayniteId=game?.PlayniteId??string.Empty,Kind=kind,Source=source,
                ArchivePath=archive,OriginalPath=path,CapturedUtc=captured,SizeBytes=info.Length,Sha256=hash,
                CloudState=game==null?"NotApplicable":"Pending",ClassificationState=game==null?"Inbox":"Assigned",ClassificationReason=classificationReason
            },token).ConfigureAwait(false);
            return true;
        }
        catch(OperationCanceledException){throw;}
        catch(Exception ex)
        {
            _logger.LogWarning(ex,"Could not archive media candidate {Path}",path);
            return false;
        }
    }

    private async Task<GameSessionEventDto?> ResolveSharedSessionAsync(string sessionId,CancellationToken token)
    {
        if(string.IsNullOrWhiteSpace(sessionId))return null;
        var session=await _store.GetSessionAsync(sessionId,token).ConfigureAwait(false);
        if(session==null)return null;
        var stop=session.StoppedUtc??DateTime.UtcNow;
        if(await _store.HasOverlappingGameSessionAsync(session.PlayniteId,session.StartedUtc.AddMinutes(-2),stop.AddMinutes(10),token).ConfigureAwait(false))
        {
            _logger.LogInformation("Skipped time-only shared media attribution for {Game} because another game session overlaps",session.PlayniteId);
            return null;
        }
        return session;
    }

    private static SharedMediaResolution ResolveSharedTarget(string path,IReadOnlyList<GameDescriptorDto> games,GameSessionEventDto? session)
    {
        var nameMatches=games.Where(x=>SharedFileMatchesGame(path,x.Name)).Take(3).ToList();
        if(nameMatches.Count==1)
            return new SharedMediaResolution(nameMatches[0],"文件名唯一匹配游戏");

        if(session!=null&&SharedFileWithinSession(path,session))
        {
            var sessionGame=games.FirstOrDefault(x=>string.Equals(x.PlayniteId,session.PlayniteId,StringComparison.OrdinalIgnoreCase));
            if(sessionGame!=null&&nameMatches.Count==0)
                return new SharedMediaResolution(sessionGame,"文件时间位于无重叠游戏会话窗口");
            if(sessionGame!=null&&nameMatches.Any(x=>string.Equals(x.PlayniteId,sessionGame.PlayniteId,StringComparison.OrdinalIgnoreCase)))
                return new SharedMediaResolution(sessionGame,"会话窗口消解了文件名的多游戏歧义");
        }

        var reason=nameMatches.Count>1
            ? "文件名同时匹配多个游戏，未自动归类"
            : session==null
                ? "缺少可安全使用的无重叠游戏会话，且文件名不能唯一匹配"
                : "文件时间不在当前无重叠游戏会话窗口内，且文件名不能唯一匹配";
        return new SharedMediaResolution(null,reason);
    }

    private async Task<List<MediaSource>> DiscoverGameSourcesAsync(GameDescriptorDto game,CancellationToken token)
    {
        var output=new List<MediaSource>();
        if(game.Platform==GamePlatformKind.Steam&&!string.IsNullOrWhiteSpace(game.PlatformGameId))
        {
            foreach(var steamRoot in SteamRoots())
            {
                var userdata=Path.Combine(steamRoot,"userdata");
                if(!Directory.Exists(userdata))continue;
                foreach(var user in Directory.EnumerateDirectories(userdata))
                    output.Add(new MediaSource(Path.Combine(user,"760","remote",game.PlatformGameId,"screenshots"),MediaSourceKind.Steam));
            }
        }
        if(!string.IsNullOrWhiteSpace(game.InstallDirectory))
            foreach(var child in new[]{"Screenshots","Screenshot","Captures","Capture","Media"})
                output.Add(new MediaSource(Path.Combine(game.InstallDirectory,child),PlatformSource(game.Platform)));
        foreach(var action in game.Actions)
        {
            var basePath=string.IsNullOrWhiteSpace(action.WorkingDirectory)?Path.GetDirectoryName(action.Path):action.WorkingDirectory;
            if(string.IsNullOrWhiteSpace(basePath))continue;
            foreach(var child in new[]{"Screenshots","Captures"})
                output.Add(new MediaSource(Path.Combine(basePath,child),action.IsModLoader?MediaSourceKind.Custom:PlatformSource(game.Platform)));
        }
        foreach(var custom in await _store.GetMediaSourcesAsync(game.PlayniteId,token).ConfigureAwait(false))
            if(custom.Enabled&&!custom.SharedDirectory&&!string.IsNullOrWhiteSpace(custom.RootPath))
                output.Add(new MediaSource(custom.RootPath,custom.SourceKind,string.IsNullOrWhiteSpace(custom.IncludePattern)?"*":custom.IncludePattern));
        return output;
    }

    private async Task<List<MediaSource>> DiscoverSharedSourcesAsync(CancellationToken token)
    {
        var output=new List<MediaSource>();
        var captures=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),"Captures");
        if(Directory.Exists(captures))output.Add(new MediaSource(captures,MediaSourceKind.XboxGameBar));
        var windowsScreens=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),"Screenshots");
        if(Directory.Exists(windowsScreens))output.Add(new MediaSource(windowsScreens,MediaSourceKind.WindowsScreenshot));
        foreach(var custom in await _store.GetSharedMediaSourcesAsync(token).ConfigureAwait(false))
            if(!string.IsNullOrWhiteSpace(custom.RootPath))
                output.Add(new MediaSource(custom.RootPath,custom.SourceKind,string.IsNullOrWhiteSpace(custom.IncludePattern)?"*":custom.IncludePattern));
        return output;
    }

    private List<(string Path,MediaSourceKind Source)> EnumerateCandidates(IEnumerable<MediaSource> sources)
    {
        var output=new List<(string Path,MediaSourceKind Source)>();
        foreach(var source in sources)
        {
            try
            {
                output.AddRange(Directory.EnumerateFiles(source.Path,string.IsNullOrWhiteSpace(source.IncludePattern)?"*":source.IncludePattern,SearchOption.AllDirectories)
                    .Where(IsMedia).Select(x=>(x,source.Source)));
            }
            catch(Exception ex){_logger.LogWarning(ex,"Could not scan media source {Path}",source.Path);}
        }
        return output.DistinctBy(x=>x.Path,StringComparer.OrdinalIgnoreCase).ToList();
    }

    private IEnumerable<string> SteamRoots()
    {
        var values=new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Steam"),@"C:\Steam",@"D:\Steam",@"E:\Steam"};
        return values.Where(Directory.Exists);
    }

    private static MediaSourceKind PlatformSource(GamePlatformKind platform)=>platform switch
    {GamePlatformKind.Steam=>MediaSourceKind.Steam,GamePlatformKind.Xbox=>MediaSourceKind.XboxGameBar,GamePlatformKind.Epic=>MediaSourceKind.Epic,GamePlatformKind.Ubisoft=>MediaSourceKind.Ubisoft,GamePlatformKind.Ea=>MediaSourceKind.Ea,GamePlatformKind.Gog=>MediaSourceKind.Gog,_=>MediaSourceKind.GameNative};

    private static bool SharedFileMatchesGame(string path,string gameName)
    {
        var file=NameNormalizer.Normalize(Path.GetFileNameWithoutExtension(path));
        var game=NameNormalizer.Normalize(gameName);
        if(string.IsNullOrWhiteSpace(file)||string.IsNullOrWhiteSpace(game))return false;
        if(file.Contains(game,StringComparison.OrdinalIgnoreCase))return true;
        var meaningful=game.Split(' ',StringSplitOptions.RemoveEmptyEntries).Where(x=>x.Length>=4).ToArray();
        return meaningful.Length>0&&meaningful.Count(x=>file.Contains(x,StringComparison.OrdinalIgnoreCase))>=Math.Min(2,meaningful.Length);
    }

    private static bool SharedFileWithinSession(string path,GameSessionEventDto? session)
    {
        if(session==null||session.StartedUtc==default)return false;
        try
        {
            var captured=SafeCapturedUtc(path);
            var start=session.StartedUtc.ToUniversalTime().AddMinutes(-2);
            var stop=(session.StoppedUtc??DateTime.UtcNow).ToUniversalTime().AddMinutes(10);
            return captured>=start&&captured<=stop;
        }
        catch{return false;}
    }

    private static DateTime SafeCapturedUtc(string path)
    {
        try
        {
            var info=new FileInfo(path);
            return info.CreationTimeUtc==DateTime.MinValue?info.LastWriteTimeUtc:info.CreationTimeUtc;
        }
        catch{return DateTime.MinValue;}
    }

    private static bool IsMedia(string path){var ext=Path.GetExtension(path);return ImageExtensions.Contains(ext)||VideoExtensions.Contains(ext);}

    private string BuildArchivePath(GameDescriptorDto game,MediaSourceKind source,MediaKind kind,DateTime captured,string hash,string extension)
    {
        var category=kind==MediaKind.Screenshot?"Screenshots":"Clips";
        var file=$"{captured:yyyy-MM-dd_HH-mm-ss}_{source}_{hash[..8]}{extension.ToLowerInvariant()}";
        return Path.Combine(_options.MediaArchiveDirectory,Sanitize(game.Name),category,captured.ToString("yyyy"),captured.ToString("MM"),file);
    }

    private string BuildInboxArchivePath(MediaSourceKind source,MediaKind kind,DateTime captured,string hash,string extension)
    {
        var category=kind==MediaKind.Screenshot?"Screenshots":"Clips";
        var file=$"{captured:yyyy-MM-dd_HH-mm-ss}_{source}_{hash[..8]}{extension.ToLowerInvariant()}";
        return Path.Combine(_options.MediaArchiveDirectory,"_Inbox","Pending",category,captured.ToString("yyyy"),captured.ToString("MM"),file);
    }

    private string BuildIgnoredArchivePath(MediaItemDto item,string extension)
    {
        var category=item.Kind==MediaKind.Screenshot?"Screenshots":"Clips";
        var file=$"{item.CapturedUtc:yyyy-MM-dd_HH-mm-ss}_{item.Source}_{item.Sha256[..8]}{extension.ToLowerInvariant()}";
        return Path.Combine(_options.MediaArchiveDirectory,"_Inbox","Ignored",category,item.CapturedUtc.ToString("yyyy"),item.CapturedUtc.ToString("MM"),file);
    }

    private static async Task RelocateArchivedCopyAsync(MediaItemDto item,string destination,CancellationToken token)
    {
        if(File.Exists(item.ArchivePath))
        {
            await MoveArchivedFileAsync(item.ArchivePath,destination,item.Sha256,token).ConfigureAwait(false);
            return;
        }
        if(File.Exists(item.OriginalPath))
        {
            // The original capture belongs to the source application/user. Rebuild the missing archive
            // copy without deleting or moving that original file.
            await EnsureArchivedCopyAsync(item.OriginalPath,destination,item.Sha256,token).ConfigureAwait(false);
            return;
        }
        throw new FileNotFoundException("归档文件和原始媒体文件都不存在，无法移动该记录。",item.ArchivePath);
    }

    private static async Task MoveArchivedFileAsync(string source,string destination,string expectedHash,CancellationToken token)
    {
        if(string.Equals(Path.GetFullPath(source),Path.GetFullPath(destination),StringComparison.OrdinalIgnoreCase))return;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if(File.Exists(destination))
        {
            var destinationHash=await ComputeSha256Async(destination,token).ConfigureAwait(false);
            if(!string.Equals(destinationHash,expectedHash,StringComparison.OrdinalIgnoreCase))
                throw new IOException("目标媒体路径已存在不同内容，已停止归类以避免覆盖。");
            File.Delete(source);
            return;
        }
        try{File.Move(source,destination);}
        catch(IOException)
        {
            await CopyAtomicallyAsync(source,destination,token).ConfigureAwait(false);
            File.Delete(source);
        }
    }

    private static async Task EnsureArchivedCopyAsync(string source,string destination,string expectedHash,CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if(File.Exists(destination))
        {
            var destinationHash=await ComputeSha256Async(destination,token).ConfigureAwait(false);
            if(!string.Equals(destinationHash,expectedHash,StringComparison.OrdinalIgnoreCase))
                throw new IOException("目标媒体路径已存在不同内容，已停止归类以避免覆盖。");
            return;
        }
        await CopyAtomicallyAsync(source,destination,token).ConfigureAwait(false);
    }

    private static async Task<bool> IsStableAsync(string path,CancellationToken token)
    {
        try
        {
            var first=new FileInfo(path).Length;await Task.Delay(350,token).ConfigureAwait(false);var second=new FileInfo(path).Length;
            return first==second&&second>0;
        }
        catch{return false;}
    }

    private static async Task<string> ComputeSha256Async(string path,CancellationToken token)
    {
        await using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite,1024*128,FileOptions.Asynchronous|FileOptions.SequentialScan);
        var hash=await SHA256.HashDataAsync(stream,token).ConfigureAwait(false);return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task CopyAtomicallyAsync(string source,string destination,CancellationToken token)
    {
        var temp=destination+".partial";if(File.Exists(temp))File.Delete(temp);
        await using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.ReadWrite,1024*128,true))
        await using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,1024*128,true))
            await input.CopyToAsync(output,token).ConfigureAwait(false);
        File.Move(temp,destination,false);
    }

    private static string Sanitize(string value)
    {
        var invalid=Path.GetInvalidFileNameChars();var normalized=new string(value.Select(c=>invalid.Contains(c)?'_':c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(normalized)?"Unknown Game":normalized;
    }

    private sealed record MediaSource(string Path,MediaSourceKind Source,string IncludePattern="*");
    private sealed record SharedMediaResolution(GameDescriptorDto? Game,string Reason);
}

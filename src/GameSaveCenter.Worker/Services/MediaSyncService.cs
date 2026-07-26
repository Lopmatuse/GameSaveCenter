using System.Security.Cryptography;
using GameSaveCenter.Contracts;
using GameSaveCenter.Core.Services;
using GameSaveCenter.Worker.Configuration;
using GameSaveCenter.Worker.Infrastructure;
using GameSaveCenter.Worker.Persistence;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Services;

/// <summary>
/// Incrementally copies screenshots and clips into a stable archive. Files are deduplicated
/// by SHA-256 and source deletion never removes the archive copy.
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
        var games=await _catalog.GetGamesAsync(token).ConfigureAwait(false);
        if(request.PlayniteIds.Count>0) games=games.Where(x=>request.PlayniteIds.Contains(x.PlayniteId,StringComparer.OrdinalIgnoreCase)).ToList();
        var output=new List<TaskStatusDto>();
        foreach(var game in games)
        {
            output.Add(await _tasks.RunAsync("MediaSync",game.PlayniteId,game.Name,async(progress,ct)=>
            {
                await progress.ReportAsync(5,"正在查找媒体来源").ConfigureAwait(false);
                var sources=DiscoverSources(game).DistinctBy(x=>x.Path,StringComparer.OrdinalIgnoreCase).Where(x=>Directory.Exists(x.Path)).ToList();
                var candidates=new List<(string Path,MediaSourceKind Source,bool Shared)>();
                foreach(var source in sources)
                {
                    try
                    {
                        candidates.AddRange(Directory.EnumerateFiles(source.Path,"*",SearchOption.AllDirectories)
                            .Where(IsMedia)
                            .Where(x=>!source.SharedDirectory||SharedFileMatchesGame(x,game.Name))
                            .Select(x=>(x,source.Source,source.SharedDirectory)));
                    }
                    catch(Exception ex){_logger.LogWarning(ex,"Could not scan media source {Path}",source.Path);}
                }

                var copied=0;var index=0;
                foreach(var candidate in candidates.OrderBy(x=>x.Path,StringComparer.OrdinalIgnoreCase))
                {
                    ct.ThrowIfCancellationRequested();index++;
                    if(!await IsStableAsync(candidate.Path,ct).ConfigureAwait(false)) continue;
                    var hash=await ComputeSha256Async(candidate.Path,ct).ConfigureAwait(false);
                    if(await _store.MediaHashExistsAsync(hash,ct).ConfigureAwait(false)) continue;
                    var info=new FileInfo(candidate.Path);
                    var captured=info.CreationTimeUtc==DateTime.MinValue?info.LastWriteTimeUtc:info.CreationTimeUtc;
                    var kind=ImageExtensions.Contains(info.Extension)?MediaKind.Screenshot:MediaKind.VideoClip;
                    var archive=BuildArchivePath(game,candidate.Source,kind,captured,hash,info.Extension);
                    Directory.CreateDirectory(Path.GetDirectoryName(archive)!);
                    await CopyAtomicallyAsync(candidate.Path,archive,ct).ConfigureAwait(false);
                    await _store.AddMediaAsync(new MediaItemDto
                    {
                        MediaId=Guid.NewGuid().ToString("N"),PlayniteId=game.PlayniteId,Kind=kind,Source=candidate.Source,
                        ArchivePath=archive,OriginalPath=candidate.Path,CapturedUtc=captured,SizeBytes=info.Length,Sha256=hash,CloudState="Pending"
                    },ct).ConfigureAwait(false);
                    copied++;
                    if(index%20==0) await progress.ReportAsync(Math.Min(85,5+(int)(80d*index/Math.Max(1,candidates.Count))),$"已检查 {index}/{candidates.Count}").ConfigureAwait(false);
                }

                var policy=await _store.GetPolicyAsync(game.PlayniteId,ct).ConfigureAwait(false);
                if((request.UploadAfterSync||policy.UploadAfterBackup)&&copied>0&&_rclone.IsConfigured)
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
            },token).ConfigureAwait(false));
        }
        return output;
    }

    private IEnumerable<MediaSource> DiscoverSources(GameDescriptorDto game)
    {
        if(game.Platform==GamePlatformKind.Steam&&!string.IsNullOrWhiteSpace(game.PlatformGameId))
        {
            foreach(var steamRoot in SteamRoots())
            {
                var userdata=Path.Combine(steamRoot,"userdata");
                if(!Directory.Exists(userdata)) continue;
                foreach(var user in Directory.EnumerateDirectories(userdata))
                    yield return new MediaSource(Path.Combine(user,"760","remote",game.PlatformGameId,"screenshots"),MediaSourceKind.Steam);
            }
        }

        // Game Bar uses one shared folder. Session timing and filename matching are used by
        // later reliability passes; MVP archives files only when the name identifies the game.
        var captures=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),"Captures");
        if(Directory.Exists(captures))
            yield return new MediaSource(captures,MediaSourceKind.XboxGameBar,true);

        var windowsScreens=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),"Screenshots");
        if(Directory.Exists(windowsScreens))
            yield return new MediaSource(windowsScreens,MediaSourceKind.WindowsScreenshot,true);

        if(!string.IsNullOrWhiteSpace(game.InstallDirectory))
        {
            foreach(var child in new[]{"Screenshots","Screenshot","Captures","Capture","Media"})
                yield return new MediaSource(Path.Combine(game.InstallDirectory,child),PlatformSource(game.Platform));
        }
        foreach(var action in game.Actions)
        {
            var basePath=string.IsNullOrWhiteSpace(action.WorkingDirectory)?Path.GetDirectoryName(action.Path):action.WorkingDirectory;
            if(string.IsNullOrWhiteSpace(basePath)) continue;
            foreach(var child in new[]{"Screenshots","Captures"}) yield return new MediaSource(Path.Combine(basePath,child),action.IsModLoader?MediaSourceKind.Custom:PlatformSource(game.Platform));
        }
    }

    private IEnumerable<string> SteamRoots()
    {
        var values=new[]{
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Steam"),
            @"C:\Steam",@"D:\Steam",@"E:\Steam"
        };
        return values.Where(Directory.Exists);
    }

    private static MediaSourceKind PlatformSource(GamePlatformKind platform)=>platform switch
    { GamePlatformKind.Steam=>MediaSourceKind.Steam,GamePlatformKind.Xbox=>MediaSourceKind.XboxGameBar,GamePlatformKind.Epic=>MediaSourceKind.Epic,
      GamePlatformKind.Ubisoft=>MediaSourceKind.Ubisoft,GamePlatformKind.Ea=>MediaSourceKind.Ea,GamePlatformKind.Gog=>MediaSourceKind.Gog,_=>MediaSourceKind.GameNative };

    private static bool SharedFileMatchesGame(string path,string gameName)
    {
        var file=NameNormalizer.Normalize(Path.GetFileNameWithoutExtension(path));
        var game=NameNormalizer.Normalize(gameName);
        if(string.IsNullOrWhiteSpace(file)||string.IsNullOrWhiteSpace(game))return false;
        if(file.Contains(game,StringComparison.OrdinalIgnoreCase))return true;
        var meaningful=game.Split(' ',StringSplitOptions.RemoveEmptyEntries).Where(x=>x.Length>=4).ToArray();
        return meaningful.Length>0&&meaningful.Count(x=>file.Contains(x,StringComparison.OrdinalIgnoreCase))>=Math.Min(2,meaningful.Length);
    }

    private static bool IsMedia(string path){var ext=Path.GetExtension(path);return ImageExtensions.Contains(ext)||VideoExtensions.Contains(ext);}

    private string BuildArchivePath(GameDescriptorDto game,MediaSourceKind source,MediaKind kind,DateTime captured,string hash,string extension)
    {
        var category=kind==MediaKind.Screenshot?"Screenshots":"Clips";
        var file=$"{captured:yyyy-MM-dd_HH-mm-ss}_{source}_{hash[..8]}{extension.ToLowerInvariant()}";
        return Path.Combine(_options.MediaArchiveDirectory,Sanitize(game.Name),category,captured.ToString("yyyy"),captured.ToString("MM"),file);
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

    private sealed record MediaSource(string Path,MediaSourceKind Source,bool SharedDirectory=false);
}

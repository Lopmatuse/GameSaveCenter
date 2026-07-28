using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using GameSaveCenter.Contracts;
using GameSaveCenter.Core.Services;
using GameSaveCenter.Worker.Configuration;
using GameSaveCenter.Worker.Persistence;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Services;

/// <summary>Imports, launches and tracks local trainers and Cheat Engine tables.</summary>
public sealed class GameToolService
{
    private readonly WorkerOptions _options;
    private readonly SqliteStateStore _store;
    private readonly ITrainerCatalogSource _catalog;
    private readonly TaskCoordinator _tasks;
    private readonly ILogger<GameToolService> _logger;
    private readonly ConcurrentDictionary<string,List<LaunchedTool>> _sessionProcesses=new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string,CancellationTokenSource> _sessionDelays=new(StringComparer.OrdinalIgnoreCase);

    public GameToolService(WorkerOptions options,SqliteStateStore store,ITrainerCatalogSource catalog,TaskCoordinator tasks,ILogger<GameToolService> logger)
    {_options=options;_store=store;_catalog=catalog;_tasks=tasks;_logger=logger;}

    public Task<List<GameToolDto>> ListAsync(string gameId,CancellationToken token)=>_store.GetGameToolsAsync(gameId,token);

    public async Task<GameToolDto> ImportAsync(ImportGameToolRequestDto request,CancellationToken token)
    {
        if(string.IsNullOrWhiteSpace(request.PlayniteId))throw new ArgumentException("必须选择目标游戏。");
        var source=Path.GetFullPath(Environment.ExpandEnvironmentVariables(request.SourcePath??string.Empty));
        if(!File.Exists(source)&&!Directory.Exists(source))throw new FileNotFoundException("导入源不存在。",source);
        if(request.ToolType==GameToolType.CheatTable&&!source.EndsWith(".ct",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cheat Table 必须是 .ct 文件。");

        var toolId=Guid.NewGuid().ToString("N");var versionId=Guid.NewGuid().ToString("N");
        var root=Path.Combine(_options.GameToolsDirectory,SafeSegment(request.PlayniteId),toolId,versionId);
        Directory.CreateDirectory(root);
        string entry;
        if(File.Exists(source)&&source.EndsWith(".zip",StringComparison.OrdinalIgnoreCase))
        {
            ExtractZipSafely(source,root);
            entry=SelectEntry(root,request.EntryFileName,request.ToolType);
        }
        else if(Directory.Exists(source))
        {
            CopyDirectory(source,root,token);
            entry=SelectEntry(root,request.EntryFileName,request.ToolType);
        }
        else if(request.CopyIntoLibrary)
        {
            entry=Path.Combine(root,Path.GetFileName(source));File.Copy(source,entry,false);
        }
        else entry=source;

        var now=DateTime.UtcNow;
        var tool=new GameToolDto
        {
            ToolId=toolId,PlayniteId=request.PlayniteId,ToolType=request.ToolType,SourceType=GameToolSourceType.Manual,
            DisplayName=string.IsNullOrWhiteSpace(request.DisplayName)?Path.GetFileNameWithoutExtension(entry):request.DisplayName.Trim(),
            Enabled=true,AutoStart=false,LaunchDelaySeconds=8,ActiveVersionId=versionId,CreatedUtc=now,UpdatedUtc=now
        };
        var version=new GameToolVersionDto
        {
            VersionId=versionId,ToolId=toolId,VersionName=string.IsNullOrWhiteSpace(request.VersionName)?"本地版本":request.VersionName.Trim(),
            EntryPath=entry,WorkingDirectory=Path.GetDirectoryName(entry)??root,FileSha256=await HashAsync(entry,token).ConfigureAwait(false),
            CreatedUtc=now,IsAvailable=true
        };
        await _store.UpsertGameToolAsync(tool,version,token).ConfigureAwait(false);
        await _store.AppendAuditAsync("GameTool","已导入游戏工具",System.Text.Json.JsonSerializer.Serialize(new{tool.ToolId,tool.PlayniteId,tool.DisplayName,tool.ToolType}),token).ConfigureAwait(false);
        return (await _store.GetGameToolAsync(toolId,token).ConfigureAwait(false))!;
    }

    public async Task<object> UpdateAsync(UpdateGameToolRequestDto request,CancellationToken token)
    {
        if(await _store.GetGameToolAsync(request.ToolId,token).ConfigureAwait(false)==null)throw new KeyNotFoundException("游戏工具不存在。");
        await _store.UpdateGameToolAsync(request,token).ConfigureAwait(false);return new{updated=true};
    }

    public async Task<object> DeleteAsync(string toolId,CancellationToken token)
    {
        if(await _store.GetGameToolAsync(toolId,token).ConfigureAwait(false)==null)throw new KeyNotFoundException("游戏工具不存在。");
        await _store.DeleteGameToolAsync(toolId,token).ConfigureAwait(false);
        await _store.AppendAuditAsync("GameTool","已解除游戏工具绑定",System.Text.Json.JsonSerializer.Serialize(new{toolId}),token).ConfigureAwait(false);
        return new{deleted=true};
    }

    public async Task<object> LaunchAsync(string toolId,CancellationToken token)
    {
        var tool=await _store.GetGameToolAsync(toolId,token).ConfigureAwait(false)??throw new KeyNotFoundException("游戏工具不存在。");
        var process=Launch(tool);return new{started=true,processId=process.Id};
    }

    public async Task<object> OpenDirectoryAsync(string toolId,CancellationToken token)
    {
        var tool=await _store.GetGameToolAsync(toolId,token).ConfigureAwait(false)??throw new KeyNotFoundException("游戏工具不存在。");
        var directory=Path.GetDirectoryName(tool.ActiveVersion.EntryPath);
        if(string.IsNullOrWhiteSpace(directory)||!Directory.Exists(directory))throw new DirectoryNotFoundException(directory);
        Process.Start(new ProcessStartInfo{FileName=directory,UseShellExecute=true});return new{opened=true};
    }

    public async Task StartAutomaticAsync(GameSessionEventDto session,CancellationToken token)
    {
        if(string.IsNullOrWhiteSpace(session.SessionId))return;
        var descriptor=await _store.GetGameAsync(session.PlayniteId,token).ConfigureAwait(false);
        if(HasAntiCheat(descriptor))
        {
            await _store.AppendAuditAsync("GameTool","检测到常见反作弊，已阻止自动启动游戏工具",
                System.Text.Json.JsonSerializer.Serialize(new{session.PlayniteId,session.GameName}),token).ConfigureAwait(false);
            return;
        }
        var tools=(await _store.GetGameToolsAsync(session.PlayniteId,token).ConfigureAwait(false)).Where(x=>x.Enabled&&x.AutoStart).ToList();
        if(tools.Count==0)return;
        var linked=CancellationTokenSource.CreateLinkedTokenSource(token);_sessionDelays[session.SessionId]=linked;
        foreach(var tool in tools)_=LaunchAfterDelayAsync(session,tool,linked.Token);
    }

    public async Task StopAutomaticAsync(string sessionId,CancellationToken token)
    {
        if(_sessionDelays.TryRemove(sessionId,out var delay)){delay.Cancel();delay.Dispose();}
        if(!_sessionProcesses.TryRemove(sessionId,out var launched))return;
        foreach(var item in launched.Where(x=>x.CloseOnExit))
        {
            try
            {
                using var process=Process.GetProcessById(item.ProcessId);
                if(process.StartTime.ToUniversalTime()<item.StartedUtc.AddSeconds(-3))continue;
                process.CloseMainWindow();
                if(!process.WaitForExit(2500))process.Kill();
            }
            catch(Exception ex){_logger.LogWarning(ex,"Could not close game tool PID {Pid}",item.ProcessId);}
        }
        await Task.CompletedTask;
    }

    public async Task<TaskStatusDto> DownloadAsync(DownloadTrainerRequestDto request,CancellationToken token)
    {
        var game=await _store.GetGameAsync(request.PlayniteId,token).ConfigureAwait(false);
        return await _tasks.RunAsync("TrainerDownload",request.PlayniteId,game?.Name??"游戏",async(progress,taskToken)=>
        {
            var catalog=await _store.GetTrainerCatalogItemAsync(request.CatalogId,taskToken).ConfigureAwait(false)
                        ??throw new KeyNotFoundException("FLiNG 目录项不存在。");
            var release=await _store.GetTrainerReleaseAsync(request.ReleaseId,taskToken).ConfigureAwait(false)
                        ??throw new KeyNotFoundException("FLiNG 版本不存在。");
            var installed=await _store.GetGameToolsAsync(request.PlayniteId,taskToken).ConfigureAwait(false);
            var existingTool=installed.FirstOrDefault(x=>x.SourceType==GameToolSourceType.Fling&&
                string.Equals(x.DisplayName,catalog.Title,StringComparison.OrdinalIgnoreCase));
            if(existingTool?.Versions.Any(x=>string.Equals(x.SourceUrl,release.DownloadUrl,StringComparison.OrdinalIgnoreCase))==true)
            {
                await progress.ReportAsync(100,"该 FLiNG 版本已经绑定，无需重复下载").ConfigureAwait(false);
                return;
            }
            var temporary=Path.Combine(_options.DownloadDirectory,request.ReleaseId+".download");
            await progress.ReportAsync(5,"正在下载 FLiNG 修改器").ConfigureAwait(false);
            var sink=new Progress<(long Received,long? Total)>(value=>
            {
                var percent=value.Total>0?(int)Math.Min(80,5+value.Received*75/value.Total.Value):35;
                _=progress.ReportAsync(percent,"正在下载 FLiNG 修改器");
            });
            await _catalog.DownloadAsync(request.ReleaseId,temporary,sink,taskToken).ConfigureAwait(false);
            await progress.ReportAsync(82,"正在安全解压").ConfigureAwait(false);
            var toolId=existingTool?.ToolId??Guid.NewGuid().ToString("N");var versionId=Guid.NewGuid().ToString("N");
            var root=Path.Combine(_options.GameToolsDirectory,SafeSegment(request.PlayniteId),toolId,versionId);Directory.CreateDirectory(root);
            string entry;
            try
            {
                if(HasSignature(temporary,0x50,0x4B))
                {
                    ExtractZipSafely(temporary,root);
                    entry=SelectEntry(root,string.Empty,GameToolType.Trainer);
                }
                else if(HasSignature(temporary,0x4D,0x5A))
                {
                    entry=Path.Combine(root,SafeSegment(release.DisplayName)+".exe");
                    File.Move(temporary,entry,true);
                }
                else
                {
                    throw new WorkerOperationException("FLING_DOWNLOAD_INVALID","下载内容既不是 ZIP 也不是 Windows 可执行文件，已拒绝绑定。",release.DownloadUrl);
                }
            }
            finally
            {
                if(File.Exists(temporary))File.Delete(temporary);
            }
            var now=DateTime.UtcNow;
            var tool=existingTool??new GameToolDto{ToolId=toolId,PlayniteId=request.PlayniteId,ToolType=GameToolType.Trainer,SourceType=GameToolSourceType.Fling,
                DisplayName=catalog.Title,Enabled=true,AutoStart=false,LaunchDelaySeconds=8,CreatedUtc=now};
            tool.ActiveVersionId=versionId;tool.UpdatedUtc=now;
            var version=new GameToolVersionDto{VersionId=versionId,ToolId=toolId,VersionName=release.DisplayName,EntryPath=entry,
                WorkingDirectory=Path.GetDirectoryName(entry)??root,SourceUrl=release.DownloadUrl,FileSha256=await HashAsync(entry,taskToken).ConfigureAwait(false),
                DownloadUtc=now,CreatedUtc=now,IsAvailable=true};
            await _store.UpsertGameToolAsync(tool,version,taskToken).ConfigureAwait(false);
            await progress.ReportAsync(96,"已下载并绑定到当前游戏").ConfigureAwait(false);
        },token).ConfigureAwait(false);
    }

    private async Task LaunchAfterDelayAsync(GameSessionEventDto session,GameToolDto tool,CancellationToken token)
    {
        try
        {
            var delay=tool.LaunchTiming==GameToolLaunchTiming.Delayed?Math.Clamp(tool.LaunchDelaySeconds,0,300):0;
            if(delay>0)await Task.Delay(TimeSpan.FromSeconds(delay),token).ConfigureAwait(false);
            var process=Launch(tool);
            var list=_sessionProcesses.GetOrAdd(session.SessionId,_=>new List<LaunchedTool>());
            lock(list)list.Add(new LaunchedTool(process.Id,DateTime.UtcNow,tool.CloseOnGameExit));
            await _store.AppendAuditAsync("GameTool","已随游戏启动工具",
                System.Text.Json.JsonSerializer.Serialize(new{session.SessionId,tool.ToolId,tool.DisplayName,processId=process.Id}),CancellationToken.None).ConfigureAwait(false);
        }
        catch(OperationCanceledException){}
        catch(Exception ex)
        {
            _logger.LogError(ex,"Automatic game tool launch failed for {Tool}",tool.DisplayName);
            await _store.AppendAuditAsync("GameTool","随游戏启动工具失败",
                System.Text.Json.JsonSerializer.Serialize(new{tool.ToolId,tool.DisplayName,error=ex.Message}),CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static Process Launch(GameToolDto tool)
    {
        var version=tool.ActiveVersion;
        if(!File.Exists(version.EntryPath))throw new FileNotFoundException("工具文件不存在，可能已被移动或安全软件隔离。",version.EntryPath);
        if(tool.ToolType!=GameToolType.CheatTable&&!version.EntryPath.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("修改器启动文件必须是 EXE。");
        var start=new ProcessStartInfo{FileName=version.EntryPath,Arguments=version.Arguments??string.Empty,
            WorkingDirectory=Directory.Exists(version.WorkingDirectory)?version.WorkingDirectory:Path.GetDirectoryName(version.EntryPath)??string.Empty,
            UseShellExecute=true};
        if(tool.RequiresAdmin)start.Verb="runas";
        return Process.Start(start)??throw new InvalidOperationException("Windows 未返回工具进程。");
    }

    private static void ExtractZipSafely(string archive,string destination)
    {
        using var zip=ZipFile.OpenRead(archive);
        foreach(var entry in zip.Entries)
        {
            string target;
            try{target=ArchivePathGuard.ResolveEntryPath(destination,entry.FullName);}
            catch(InvalidDataException ex){throw new InvalidDataException("ZIP 包含越界路径，已拒绝解压。",ex);}
            if(string.IsNullOrEmpty(entry.Name)){Directory.CreateDirectory(target);continue;}
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);entry.ExtractToFile(target,false);
        }
    }

    private static string SelectEntry(string root,string requested,GameToolType type)
    {
        if(!string.IsNullOrWhiteSpace(requested))
        {
            var selected=Path.GetFullPath(Path.Combine(root,requested));
            if(selected.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&File.Exists(selected))return selected;
        }
        var extension=type==GameToolType.CheatTable?"*.ct":"*.exe";
        var candidates=Directory.GetFiles(root,extension,SearchOption.AllDirectories)
            .Where(x=>!Path.GetFileName(x).Contains("unins",StringComparison.OrdinalIgnoreCase)&&!Path.GetFileName(x).Contains("update",StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x=>new FileInfo(x).Length).ToList();
        if(candidates.Count==0)throw new InvalidDataException($"未在导入内容中找到 {extension}。");
        return candidates[0];
    }

    private static void CopyDirectory(string source,string target,CancellationToken token)
    {
        foreach(var directory in Directory.GetDirectories(source,"*",SearchOption.AllDirectories))
        {token.ThrowIfCancellationRequested();Directory.CreateDirectory(Path.Combine(target,Path.GetRelativePath(source,directory)));}
        foreach(var file in Directory.GetFiles(source,"*",SearchOption.AllDirectories))
        {token.ThrowIfCancellationRequested();var destination=Path.Combine(target,Path.GetRelativePath(source,file));Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Copy(file,destination,false);}
    }

    private static async Task<string> HashAsync(string path,CancellationToken token)
    {
        await using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,81920,true);
        using var hash=SHA256.Create();var value=await hash.ComputeHashAsync(stream,token).ConfigureAwait(false);
        return Convert.ToHexString(value).ToLowerInvariant();
    }

    private static bool HasAntiCheat(GameDescriptorDto? game)
    {
        if(game==null)return false;
        var values=game.Actions.SelectMany(x=>new[]{x.Name,x.Path,x.Arguments}).Concat(game.KnownProcessNames).Concat(game.Tags);
        return values.Any(x=>!string.IsNullOrWhiteSpace(x)&&(x.Contains("easyanticheat",StringComparison.OrdinalIgnoreCase)||
            x.Contains("easy anti-cheat",StringComparison.OrdinalIgnoreCase)||x.Contains("battleye",StringComparison.OrdinalIgnoreCase)||
            x.Contains("ricochet",StringComparison.OrdinalIgnoreCase)||x.Contains("vanguard",StringComparison.OrdinalIgnoreCase)));
    }

    private static bool HasSignature(string path,byte first,byte second)
    {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        return stream.ReadByte()==first&&stream.ReadByte()==second;
    }

    private static string SafeSegment(string value)
    {
        var invalid=Path.GetInvalidFileNameChars();var clean=new string((value??string.Empty).Where(x=>!invalid.Contains(x)).ToArray());
        return string.IsNullOrWhiteSpace(clean)?"unnamed":clean.Length>80?clean[..80]:clean;
    }

    private sealed record LaunchedTool(int ProcessId,DateTime StartedUtc,bool CloseOnExit);
}

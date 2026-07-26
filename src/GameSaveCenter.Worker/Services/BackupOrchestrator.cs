using System.Text.Json;
using GameSaveCenter.Contracts;
using GameSaveCenter.Core.Services;
using GameSaveCenter.Worker.Infrastructure;
using GameSaveCenter.Worker.Configuration;
using GameSaveCenter.Worker.Persistence;

namespace GameSaveCenter.Worker.Services;

/// <summary>Coordinates safe Ludusavi backups, validation, history indexing and optional upload.</summary>
public sealed class BackupOrchestrator
{
    private readonly GameCatalogService _catalog;
    private readonly SqliteStateStore _store;
    private readonly LudusaviClient _ludusavi;
    private readonly RcloneClient _rclone;
    private readonly TaskCoordinator _tasks;
    private readonly WorkerOptions _options;
    private readonly BackupValidationService _validator=new();

    public BackupOrchestrator(GameCatalogService catalog,SqliteStateStore store,LudusaviClient ludusavi,RcloneClient rclone,TaskCoordinator tasks,WorkerOptions options)
    { _catalog=catalog;_store=store;_ludusavi=ludusavi;_rclone=rclone;_tasks=tasks;_options=options; }

    public async Task<List<TaskStatusDto>> BackupAsync(BackupRequestDto request,CancellationToken token)
    {
        var games=await _catalog.GetGamesAsync(token).ConfigureAwait(false);
        var matches=await _catalog.GetMatchesAsync(token).ConfigureAwait(false);
        if(request.PlayniteIds.Count>0) games=games.Where(x=>request.PlayniteIds.Contains(x.PlayniteId,StringComparer.OrdinalIgnoreCase)).ToList();
        var results=new List<TaskStatusDto>();
        foreach(var game in games)
        {
            if(!matches.TryGetValue(game.PlayniteId,out var match)||string.IsNullOrWhiteSpace(match.Name)) continue;
            results.Add(await _tasks.RunAsync("Backup",game.PlayniteId,game.Name,async(progress,ct)=>
            {
                await progress.ReportAsync(10,"正在扫描存档").ConfigureAwait(false);
                var operation=await _ludusavi.BackupAsync(new[]{match.Name},request.Force,false,ct).ConfigureAwait(false);
                if(!operation.Success) throw new InvalidOperationException($"{operation.ErrorCode}: {operation.ErrorMessage}");
                if(operation.Json.HasValue&&LudusaviResultParser.SomeGamesFailed(operation.Json.Value)) throw new InvalidOperationException("Ludusavi reported that this game failed to back up.");
                await progress.ReportAsync(55,"正在校验备份摘要").ConfigureAwait(false);
                var now=DateTime.UtcNow;
                var snapshot=LudusaviResultParser.ParseOperationSnapshot(operation.Json!.Value,match.Name,$"pending-{now:yyyyMMddHHmmss}",now);
                var previous=(await _store.GetBackupVersionsAsync(game.PlayniteId,ct).ConfigureAwait(false)).FirstOrDefault();
                var previousDomain=previous==null?null:new GameSaveCenter.Core.Models.BackupSnapshot{BackupId=previous.BackupId,CreatedUtc=previous.CreatedUtc,FileCount=previous.FileCount,TotalBytes=previous.TotalBytes};
                foreach(var finding in _validator.Validate(snapshot,previousDomain,null,true))
                    await _store.AddFindingAsync(game.PlayniteId,new ValidationFindingDto{Severity=finding.Severity,Code=finding.Code,Title=finding.Title,Detail=finding.Detail,SuggestedAction=finding.SuggestedAction},ct).ConfigureAwait(false);
                await progress.ReportAsync(70,"正在索引历史版本").ConfigureAwait(false);
                await RefreshBackupHistoryAsync(game.PlayniteId,match.Name,ct).ConfigureAwait(false);
                var indexed=(await _store.GetBackupVersionsAsync(game.PlayniteId,ct).ConfigureAwait(false)).OrderByDescending(x=>x.CreatedUtc).FirstOrDefault();
                if(indexed!=null)
                {
                    indexed.TotalBytes=snapshot.TotalBytes;indexed.FileCount=snapshot.FileCount;
                    await _store.AddBackupVersionAsync(indexed,JsonSerializer.Serialize(snapshot.Files),ct).ConfigureAwait(false);
                }
                var policy=await _store.GetPolicyAsync(game.PlayniteId,ct).ConfigureAwait(false);
                if(_options.EnableCloudUpload&&policy.UploadAfterBackup&&_rclone.IsConfigured)
                {
                    await progress.ReportAsync(82,"正在复制到云端").ConfigureAwait(false);
                    var cloud=await _rclone.CopyAsync(_options.LudusaviBackupDirectory,Path.Combine(Environment.MachineName,"Saves"),ct).ConfigureAwait(false);
                    if(!cloud.Success) throw new InvalidOperationException("Local backup succeeded, but cloud upload failed: "+cloud.StandardError);
                }
                await progress.ReportAsync(100,"备份完成").ConfigureAwait(false);
            },token).ConfigureAwait(false));
        }
        return results;
    }

    public async Task RefreshBackupHistoryAsync(string playniteId,string ludusaviName,CancellationToken token)
    {
        var listed=await _ludusavi.ListBackupsAsync(new[]{ludusaviName},token).ConfigureAwait(false);
        if(!listed.Success||!listed.Json.HasValue) return;
        foreach(var version in LudusaviResultParser.ParseBackupList(listed.Json.Value,playniteId,ludusaviName))
            await _store.AddBackupVersionAsync(version,"{}",token).ConfigureAwait(false);
    }
}

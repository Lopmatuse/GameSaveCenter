using System.Text.Json;
using GameSaveCenter.Contracts;
using GameSaveCenter.Worker.Configuration;
using GameSaveCenter.Core.Models;
using GameSaveCenter.Core.Services;
using GameSaveCenter.Worker.Infrastructure;
using GameSaveCenter.Worker.Persistence;
using GameSaveCenter.Worker.Services;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Ipc;

/// <summary>Maps versioned IPC requests to Worker services.</summary>
public sealed class IpcRequestDispatcher
{
    private readonly JsonSerializerOptions _json=new(JsonSerializerDefaults.Web){PropertyNameCaseInsensitive=true};
    private readonly GameCatalogService _catalog;
    private readonly GameSessionCoordinator _sessions;
    private readonly BackupOrchestrator _backup;
    private readonly RestoreOrchestrator _restore;
    private readonly MediaSyncService _media;
    private readonly SavePathDetectionService _detection;
    private readonly DashboardService _dashboard;
    private readonly SqliteStateStore _store;
    private readonly TaskCoordinator _tasks;
    private readonly LudusaviClient _ludusavi;
    private readonly WorkerOptions _options;
    private readonly GameToolService _gameTools;
    private readonly ITrainerCatalogSource _trainerCatalog;
    private readonly DeviceStateService _deviceStates;
    private readonly ILogger<IpcRequestDispatcher> _logger;

    public IpcRequestDispatcher(GameCatalogService catalog,GameSessionCoordinator sessions,BackupOrchestrator backup,RestoreOrchestrator restore,
        MediaSyncService media,SavePathDetectionService detection,DashboardService dashboard,SqliteStateStore store,TaskCoordinator tasks,
        LudusaviClient ludusavi,WorkerOptions options,GameToolService gameTools,ITrainerCatalogSource trainerCatalog,DeviceStateService deviceStates,ILogger<IpcRequestDispatcher> logger)
    { _catalog=catalog;_sessions=sessions;_backup=backup;_restore=restore;_media=media;_detection=detection;_dashboard=dashboard;_store=store;_tasks=tasks;_ludusavi=ludusavi;_options=options;_gameTools=gameTools;_trainerCatalog=trainerCatalog;_deviceStates=deviceStates;_logger=logger; }

    public async Task<IpcEnvelope> DispatchAsync(IpcEnvelope request,CancellationToken token)
    {
        if(request.ProtocolVersion!=ProtocolConstants.ProtocolVersion)return Error(request,"PROTOCOL_MISMATCH","Worker and plugin protocol versions do not match.");
        try
        {
            object payload=request.Type switch
            {
                MessageTypes.Ping=>new{utc=DateTime.UtcNow,version=typeof(IpcRequestDispatcher).Assembly.GetName().Version?.ToString()??"dev"},
                MessageTypes.GetDashboard=>await _dashboard.GetAsync(token).ConfigureAwait(false),
                MessageTypes.UpsertGames=>await UpsertAsync(Read<List<GameDescriptorDto>>(request),token).ConfigureAwait(false),
                MessageTypes.GameSessionStarted=>await _sessions.StartAsync(Read<GameSessionEventDto>(request),token).ConfigureAwait(false),
                MessageTypes.GameSessionStopped=>await StopAsync(Read<GameSessionEventDto>(request),token).ConfigureAwait(false),
                MessageTypes.BackupGame or MessageTypes.BackupAll=>await _backup.BackupAsync(Read<BackupRequestDto>(request),token).ConfigureAwait(false),
                MessageTypes.ListBackups=>await ListBackupsAsync(Read<GameQueryDto>(request),token).ConfigureAwait(false),
                MessageTypes.CompareBackups=>await CompareBackupsAsync(Read<BackupCompareRequestDto>(request),token).ConfigureAwait(false),
                MessageTypes.PreviewRetention=>await PreviewRetentionAsync(Read<GameQueryDto>(request),token).ConfigureAwait(false),
                MessageTypes.UpdateBackupMetadata=>await UpdateMetadataAsync(Read<BackupMetadataUpdateDto>(request),token).ConfigureAwait(false),
                MessageTypes.RestorePreview=>ToPortable(await _restore.PreviewAsync(Read<RestoreRequestDto>(request),token).ConfigureAwait(false)),
                MessageTypes.RestoreExecute=>await _restore.ExecuteAsync(Read<RestoreRequestDto>(request),token).ConfigureAwait(false),
                MessageTypes.UndoRestore=>await _restore.UndoAsync(Read<GameQueryDto>(request).PlayniteId,token).ConfigureAwait(false),
                MessageTypes.SyncMedia=>await _media.SyncAsync(Read<MediaSyncRequestDto>(request),token).ConfigureAwait(false),
                MessageTypes.ListMedia=>await ListMediaAsync(Read<GameQueryDto>(request),token).ConfigureAwait(false),
                MessageTypes.GetMediaSummary=>await _store.GetMediaSummaryAsync(Read<GameQueryDto>(request).PlayniteId,token).ConfigureAwait(false),
                MessageTypes.UpdateMediaMetadata=>await UpdateMediaMetadataAsync(Read<MediaMetadataUpdateDto>(request),token).ConfigureAwait(false),
                MessageTypes.UpdateMediaMetadataBatch=>await UpdateMediaMetadataBatchAsync(Read<MediaMetadataBatchUpdateDto>(request),token).ConfigureAwait(false),
                MessageTypes.ListUnassignedMedia=>await _store.GetUnassignedMediaAsync(Read<GameQueryDto>(request).Limit,token).ConfigureAwait(false),
                MessageTypes.ReassignMedia=>await _media.ReassignAsync(Read<ReassignMediaRequestDto>(request),token).ConfigureAwait(false),
                MessageTypes.IgnoreMedia=>await _media.IgnoreAsync(Read<IgnoreMediaRequestDto>(request),token).ConfigureAwait(false),
                MessageTypes.AddMediaSource=>await AddMediaSourceAsync(Read<MediaSourceRuleDto>(request),token).ConfigureAwait(false),
                MessageTypes.ListMediaSources=>await _store.GetMediaSourcesAsync(Read<GameQueryDto>(request).PlayniteId,token).ConfigureAwait(false),
                MessageTypes.DetectSavePaths=>await _detection.DetectAsync(Read<DetectionRequestDto>(request),token).ConfigureAwait(false),
                MessageTypes.ListSaveCandidates=>await _store.GetSaveCandidatesAsync(Read<GameQueryDto>(request).PlayniteId,token).ConfigureAwait(false),
                MessageTypes.AcceptSavePath=>await _detection.AcceptAsync(Read<AcceptSavePathRequestDto>(request),token).ConfigureAwait(false),
                MessageTypes.RejectSavePath=>await _detection.RejectAsync(Read<AcceptSavePathRequestDto>(request),token).ConfigureAwait(false),
                MessageTypes.ValidateGame=>await ValidateAsync(Read<ValidateGameRequestDto>(request),token).ConfigureAwait(false),
                MessageTypes.GetGamePolicy=>await _store.GetPolicyAsync(Read<GameQueryDto>(request).PlayniteId,token).ConfigureAwait(false),
                MessageTypes.UpdateGamePolicy=>await UpdatePolicyAsync(Read<GamePolicyUpdateDto>(request),token).ConfigureAwait(false),
                MessageTypes.GetTasks=>await _store.GetRecentTasksAsync(200,token).ConfigureAwait(false),
                MessageTypes.GetTaskChanges=>GetTaskChanges(Read<TaskChangeRequestDto>(request)),
                MessageTypes.WaitForTaskChanges=>await WaitForTaskChangesAsync(Read<TaskChangeRequestDto>(request),token).ConfigureAwait(false),
                MessageTypes.RetryCloudUpload=>await _backup.RetryCloudUploadAsync(Read<GameQueryDto>(request).PlayniteId,token).ConfigureAwait(false),
                MessageTypes.SyncDeviceStates=>await _deviceStates.SyncAsync(token).ConfigureAwait(false),
                MessageTypes.SaveDeviceConflictDecision=>await SaveDeviceConflictDecisionAsync(Read<DeviceConflictDecisionDto>(request),token).ConfigureAwait(false),
                MessageTypes.ListProcessMappings=>await _store.GetProcessMappingsAsync(token).ConfigureAwait(false),
                MessageTypes.SaveProcessMapping=>await SaveProcessMappingAsync(Read<ProcessMappingDto>(request),token).ConfigureAwait(false),
                MessageTypes.DeleteProcessMapping=>await DeleteProcessMappingAsync(Read<ProcessMappingDto>(request).ExecutableName,token).ConfigureAwait(false),
                MessageTypes.GetLogs=>await _store.GetAuditAsync(500,token).ConfigureAwait(false),
                MessageTypes.GetSettings=>SanitizedSettings(),
                MessageTypes.UpdateSettings=>await UpdateSettingsAsync(Read<WorkerSettingsDto>(request),token).ConfigureAwait(false),
                MessageTypes.CancelTask=>new CancelTaskResultDto{Cancelled=_tasks.Cancel(Read<CancelTaskRequestDto>(request).TaskId)},
                MessageTypes.ListGameTools=>await _gameTools.ListAsync(Read<GameQueryDto>(request).PlayniteId,token).ConfigureAwait(false),
                MessageTypes.InspectGameToolImport=>await _gameTools.InspectImportAsync(Read<InspectGameToolImportRequestDto>(request),token).ConfigureAwait(false),
                MessageTypes.ImportGameTool=>await _gameTools.ImportAsync(Read<ImportGameToolRequestDto>(request),token).ConfigureAwait(false),
                MessageTypes.UpdateGameTool=>await _gameTools.UpdateAsync(Read<UpdateGameToolRequestDto>(request),token).ConfigureAwait(false),
                MessageTypes.DeleteGameTool=>await _gameTools.DeleteAsync(Read<GameToolCommandRequestDto>(request).ToolId,token).ConfigureAwait(false),
                MessageTypes.LaunchGameTool=>await _gameTools.LaunchAsync(Read<GameToolCommandRequestDto>(request).ToolId,token).ConfigureAwait(false),
                MessageTypes.OpenGameToolDirectory=>await _gameTools.OpenDirectoryAsync(Read<GameToolCommandRequestDto>(request).ToolId,token).ConfigureAwait(false),
                MessageTypes.SyncTrainerCatalog=>await _trainerCatalog.SyncCatalogAsync(token).ConfigureAwait(false),
                MessageTypes.SearchTrainerCatalog=>await SearchTrainerCatalogAsync(Read<TrainerCatalogQueryDto>(request),token).ConfigureAwait(false),
                MessageTypes.GetTrainerReleases=>await _trainerCatalog.GetReleasesAsync(Read<TrainerCatalogQueryDto>(request).CatalogId,token).ConfigureAwait(false),
                MessageTypes.DownloadTrainer=>await _gameTools.DownloadAsync(Read<DownloadTrainerRequestDto>(request),token).ConfigureAwait(false),
                _=>throw new NotSupportedException($"Unknown IPC message type: {request.Type}")
            };
            return Success(request,payload);
        }
        catch(WorkerOperationException ex)
        {
            _logger.LogError(ex,"IPC request {Type} failed with {Code}",request.Type,ex.Code);
            var message=string.IsNullOrWhiteSpace(ex.DiagnosticDetail)?ex.Message:$"{ex.Message} | {ex.DiagnosticDetail}";
            return Error(request,ex.Code,message);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex,"IPC request {Type} failed",request.Type);
            return Error(request,ex.GetType().Name,ex.Message);
        }
    }

    private async Task<object> UpsertAsync(List<GameDescriptorDto> games,CancellationToken token){await _catalog.UpsertAndMatchAsync(games,token).ConfigureAwait(false);return new{accepted=games.Count};}
    private object GetTaskChanges(TaskChangeRequestDto request)=>_tasks.GetChanges(request.AfterSequence,request.Limit);
    private Task<TaskChangeFeedDto> WaitForTaskChangesAsync(TaskChangeRequestDto request,CancellationToken token)
        =>_tasks.WaitForChangesAsync(request.AfterSequence,request.Limit,request.WaitSeconds,token);
    private async Task<object> SaveProcessMappingAsync(ProcessMappingDto mapping,CancellationToken token)
    {
        mapping.ExecutableName=Path.GetFileNameWithoutExtension(mapping.ExecutableName??string.Empty).Trim();
        if(string.IsNullOrWhiteSpace(mapping.ExecutableName)||string.IsNullOrWhiteSpace(mapping.PlayniteId))throw new ArgumentException("必须提供 EXE 名称和目标游戏。");
        var game=await _catalog.GetGameAsync(mapping.PlayniteId,token).ConfigureAwait(false)??throw new InvalidOperationException("目标游戏不存在。");
        mapping.GameName=game.Name;mapping.CreatedUtc=DateTime.UtcNow;await _store.UpsertProcessMappingAsync(mapping,token).ConfigureAwait(false);return mapping;
    }
    private async Task<object> DeleteProcessMappingAsync(string executableName,CancellationToken token)
    { await _store.DeleteProcessMappingAsync(executableName,token).ConfigureAwait(false);return new { deleted=true }; }
    private async Task<DeviceConflictDecisionDto> SaveDeviceConflictDecisionAsync(DeviceConflictDecisionDto decision,CancellationToken token)
    {
        var allowed=new[]{"Defer","KeepBoth","PreferLocal","PreferRemote"};
        if(string.IsNullOrWhiteSpace(decision.PlayniteId)||string.IsNullOrWhiteSpace(decision.RemoteDevice))throw new ArgumentException("必须选择设备冲突记录。");
        if(!allowed.Contains(decision.Decision,StringComparer.Ordinal))throw new ArgumentException("设备冲突决策无效。");
        decision.Comment=(decision.Comment??string.Empty).Trim();
        if(decision.Comment.Length>1000)throw new ArgumentException("决策备注不能超过 1000 个字符。");
        decision.DecidedUtc=DateTime.UtcNow;
        await _store.SaveDeviceConflictDecisionAsync(decision,token).ConfigureAwait(false);
        await _store.AppendAuditAsync("DeviceConflict","已记录人工冲突决策",JsonSerializer.Serialize(decision),token).ConfigureAwait(false);
        return decision;
    }
    private async Task<object> StopAsync(GameSessionEventDto value,CancellationToken token){await _sessions.StopAsync(value,token).ConfigureAwait(false);return new{stopped=true};}
    private async Task<List<BackupVersionDto>> ListBackupsAsync(GameQueryDto query,CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(query.PlayniteId)) return new List<BackupVersionDto>();

        var cached = await _store.GetBackupVersionsAsync(query.PlayniteId, token).ConfigureAwait(false);

        // Cached history is the normal read path. Reconcile only when the cache is empty or the
        // caller explicitly asks for disk refresh; successful backup/restore tasks already update it.
        Exception? reconcileError = null;
        if (_ludusavi.IsAvailable && (query.ForceRefresh || cached.Count == 0))
        {
            try
            {
                var matches = await _catalog.GetMatchesAsync(token).ConfigureAwait(false);
                if (matches.TryGetValue(query.PlayniteId, out var match) && !string.IsNullOrWhiteSpace(match.Name))
                {
                    await _backup.RefreshBackupHistoryAsync(query.PlayniteId, match.Name, token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                reconcileError = ex;
                _logger.LogWarning(ex, "Could not reconcile backup history for {PlayniteId}", query.PlayniteId);
                await _store.AppendAuditAsync(
                    "BackupHistory",
                    "读取历史版本失败，已保留最后一次有效索引",
                    ex.ToString(),
                    token).ConfigureAwait(false);
            }
        }

        if (query.ForceRefresh || cached.Count == 0)
            cached = await _store.GetBackupVersionsAsync(query.PlayniteId, token).ConfigureAwait(false);
        if (cached.Count == 0 && reconcileError != null)
        {
            throw new WorkerOperationException(
                "BACKUP_HISTORY_REFRESH_FAILED",
                "磁盘备份可能已经存在，但读取历史索引失败；请查看诊断页中的 BackupHistory 日志。",
                reconcileError.ToString());
        }
        return cached;
    }
    private Task<List<MediaItemDto>> ListMediaAsync(GameQueryDto query,CancellationToken token)=>_store.GetMediaAsync(query.PlayniteId,query.Limit,token);
    private async Task<MediaItemDto> UpdateMediaMetadataAsync(MediaMetadataUpdateDto update,CancellationToken token)
    {
        if(string.IsNullOrWhiteSpace(update.MediaId))throw new ArgumentException("必须选择媒体。");
        update.Comment=(update.Comment??string.Empty).Trim();
        if(update.Comment.Length>1000)throw new ArgumentException("媒体备注不能超过 1000 个字符。");
        var existing=await _store.GetMediaByIdAsync(update.MediaId,token).ConfigureAwait(false)
                     ?? throw new InvalidOperationException("媒体记录不存在。");
        await _store.UpdateMediaMetadataAsync(update,token).ConfigureAwait(false);
        existing.IsFavorite=update.IsFavorite;
        existing.Comment=update.Comment;
        await _store.AppendAuditAsync("MediaMetadata","Updated media metadata",
            JsonSerializer.Serialize(new{update.MediaId,update.IsFavorite}),token).ConfigureAwait(false);
        return existing;
    }

    private async Task<List<MediaItemDto>> UpdateMediaMetadataBatchAsync(MediaMetadataBatchUpdateDto update,CancellationToken token)
    {
        update.MediaIds=(update.MediaIds??new List<string>())
            .Where(x=>!string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if(update.MediaIds.Count==0)throw new ArgumentException("必须选择至少一个媒体文件。");
        if(update.MediaIds.Count>500)throw new ArgumentException("单次最多批量更新 500 个媒体文件。");
        update.Comment=(update.Comment??string.Empty).Trim();
        if(update.UpdateComment&&update.Comment.Length>1000)throw new ArgumentException("媒体备注不能超过 1000 个字符。");
        if(!update.IsFavorite.HasValue&&!update.UpdateComment)throw new ArgumentException("没有需要更新的媒体元数据。");

        await _store.UpdateMediaMetadataBatchAsync(update,token).ConfigureAwait(false);
        var result=new List<MediaItemDto>(update.MediaIds.Count);
        foreach(var mediaId in update.MediaIds)
        {
            var item=await _store.GetMediaByIdAsync(mediaId,token).ConfigureAwait(false)
                     ?? throw new InvalidOperationException("批量更新后无法读取媒体记录。");
            result.Add(item);
        }
        await _store.AppendAuditAsync("MediaMetadata","批量更新媒体元数据",
            JsonSerializer.Serialize(new{count=result.Count,update.IsFavorite,update.UpdateComment,mediaIds=update.MediaIds}),token).ConfigureAwait(false);
        return result;
    }

    private async Task<object> UpdatePolicyAsync(GamePolicyUpdateDto update,CancellationToken token)
    {
        update.Policy ??= new BackupPolicyDto();
        update.Policy.DuringPlayIntervalMinutes = Math.Clamp(update.Policy.DuringPlayIntervalMinutes, 1, 1440);
        await _store.SetPolicyAsync(update.PlayniteId,update.Policy,token).ConfigureAwait(false);
        await _store.AppendAuditAsync("Policy","Updated game policy",JsonSerializer.Serialize(new{update.PlayniteId,update.Policy}),token).ConfigureAwait(false);
        return new{updated=true};
    }

    private async Task<object> AddMediaSourceAsync(MediaSourceRuleDto source,CancellationToken token)
    {
        source.RootPath=Path.GetFullPath(Environment.ExpandEnvironmentVariables(source.RootPath));
        if(!Directory.Exists(source.RootPath))throw new DirectoryNotFoundException(source.RootPath);
        if(string.IsNullOrWhiteSpace(source.SourceId))source.SourceId=Guid.NewGuid().ToString("N");
        await _store.AddMediaSourceAsync(source,token).ConfigureAwait(false);
        return source;
    }

    private async Task<BackupDiffDto> CompareBackupsAsync(BackupCompareRequestDto request,CancellationToken token)
    {
        var left=JsonSerializer.Deserialize<List<FileManifestEntry>>(await _store.GetBackupManifestAsync(request.PlayniteId,request.LeftBackupId,token).ConfigureAwait(false),_json)??new List<FileManifestEntry>();
        var right=JsonSerializer.Deserialize<List<FileManifestEntry>>(await _store.GetBackupManifestAsync(request.PlayniteId,request.RightBackupId,token).ConfigureAwait(false),_json)??new List<FileManifestEntry>();
        var diff=new FileManifestDiffService().Compare(left,right);
        return new BackupDiffDto
        {
            LeftBackupId=request.LeftBackupId,RightBackupId=request.RightBackupId,Added=diff.Added.Select(x=>x.RelativePath).ToList(),Removed=diff.Removed.Select(x=>x.RelativePath).ToList(),
            Modified=diff.Modified.Select(x=>x.RelativePath).ToList(),UnchangedCount=diff.Unchanged.Count,
            Summary=$"新增 {diff.Added.Count}，删除 {diff.Removed.Count}，修改 {diff.Modified.Count}，未变化 {diff.Unchanged.Count}"
        };
    }

    private async Task<RetentionPreviewDto> PreviewRetentionAsync(GameQueryDto query,CancellationToken token)
    {
        var versions=await _store.GetBackupVersionsAsync(query.PlayniteId,token).ConfigureAwait(false);
        var policy=await _store.GetPolicyAsync(query.PlayniteId,token).ConfigureAwait(false);
        var snapshots=versions.Select(x=>new BackupSnapshot{BackupId=x.BackupId,CreatedUtc=x.CreatedUtc,TotalBytes=x.TotalBytes,FileCount=x.FileCount,IsLocked=x.IsLocked,IsPreRestore=x.IsPreRestore,Comment=x.Comment,SourceDevice=x.SourceDevice}).ToList();
        var plan=new RetentionPlanner().CreatePlan(snapshots,new RetentionPolicy{KeepAllFor=TimeSpan.FromHours(policy.KeepRecentAllHours),KeepDailyDays=policy.KeepDailyDays,KeepWeeklyWeeks=policy.KeepWeeklyWeeks,KeepMonthlyMonths=policy.KeepMonthlyMonths},DateTime.UtcNow);
        return new RetentionPreviewDto{KeepBackupIds=plan.Keep.Select(x=>x.BackupId).ToList(),DeleteCandidateIds=plan.DeleteCandidates.Select(x=>x.BackupId).ToList(),Summary=$"建议保留 {plan.Keep.Count} 个版本；{plan.DeleteCandidates.Count} 个版本可由用户审核后清理。自动删除未启用。"};
    }

    private async Task<object> ValidateAsync(ValidateGameRequestDto request,CancellationToken token)
    {
        var versions=await _store.GetBackupVersionsAsync(request.PlayniteId,token).ConfigureAwait(false);
        var latest=versions.FirstOrDefault();
        if(latest==null)return new{valid=false,message="No indexed backup exists."};
        var valid=latest.FileCount>0&&latest.TotalBytes>0;
        if(!valid) await _store.AddFindingAsync(request.PlayniteId,new ValidationFindingDto
        {
            PlayniteId=request.PlayniteId,Severity=FindingSeverity.Error,Code="LATEST_BACKUP_EMPTY",Title="最新备份摘要为空",
            Detail=$"文件数 {latest.FileCount}，体积 {latest.TotalBytes} 字节。",SuggestedAction="重新运行备份并核对 Ludusavi 匹配与存档路径。"
        },token).ConfigureAwait(false);
        return new{valid,latest.BackupId,latest.FileCount,latest.TotalBytes};
    }

    private async Task<object> UpdateMetadataAsync(BackupMetadataUpdateDto update,CancellationToken token)
    {
        var matches=await _catalog.GetMatchesAsync(token).ConfigureAwait(false);
        if(!matches.TryGetValue(update.PlayniteId,out var match)||string.IsNullOrWhiteSpace(match.Name))throw new InvalidOperationException("Game is not matched to Ludusavi.");
        var edited=await _ludusavi.EditBackupAsync(match.Name,update.BackupId,update.Comment,update.Locked,token).ConfigureAwait(false);
        if(!edited.Success)throw new InvalidOperationException(edited.ErrorMessage);
        await _backup.RefreshBackupHistoryAsync(update.PlayniteId,match.Name,token).ConfigureAwait(false);
        return new{updated=true};
    }



    private async Task<object> UpdateSettingsAsync(WorkerSettingsDto settings,CancellationToken token)
    {
        _options.Apply(settings,persist:true);
        await _store.AppendAuditAsync("Settings","Worker settings updated",JsonSerializer.Serialize(new
        {
            _options.LudusaviExecutable,_options.LudusaviBackupDirectory,_options.BackupFormat,_options.FullBackupLimit,_options.DifferentialBackupLimit
        }),token).ConfigureAwait(false);
        return SanitizedSettings();
    }

    private async Task<List<TrainerCatalogItemDto>> SearchTrainerCatalogAsync(TrainerCatalogQueryDto query,CancellationToken token)
    {
        var result=await _trainerCatalog.SearchAsync(query.Query,query.Limit,token).ConfigureAwait(false);
        if(result.Count==0)
        {
            try
            {
                await _trainerCatalog.SyncCatalogAsync(token).ConfigureAwait(false);
                result=await _trainerCatalog.SearchAsync(query.Query,query.Limit,token).ConfigureAwait(false);
            }
            catch(Exception ex)
            {
                _logger.LogWarning(ex,"FLiNG catalog refresh failed; returning cached search results");
                throw new WorkerOperationException("FLING_CATALOG_UNAVAILABLE",
                    "FLiNG 目录暂不可用，且本地缓存中没有匹配结果。请稍后重试；已安装的本地工具不受影响。",ex.Message);
            }
        }
        return result;
    }

    private WorkerSettingsSnapshotDto SanitizedSettings()=>new()
    {
        DataDirectory=_options.DataDirectory,
        LudusaviExecutable=_options.LudusaviExecutable,
        LudusaviBackupDirectory=_options.LudusaviBackupDirectory,
        RcloneExecutable=_options.RcloneExecutable,
        RcloneDestinationConfigured=!string.IsNullOrWhiteSpace(_options.RcloneDestination),
        MediaArchiveDirectory=_options.MediaArchiveDirectory,
        ProcessPollingSeconds=_options.ProcessPollingSeconds,
        DefaultBackupIntervalMinutes=_options.DefaultBackupIntervalMinutes,
        EnableProcessDetection=_options.EnableProcessDetection,
        EnableSessionSavePathDetection=_options.EnableSessionSavePathDetection,
        EnableMediaSync=_options.EnableMediaSync,
        EnableCloudUpload=_options.EnableCloudUpload,
        BackupFormat=_options.BackupFormat,
        Compression=_options.Compression,
        CompressionLevel=_options.CompressionLevel,
        FullBackupLimit=_options.FullBackupLimit,
        DifferentialBackupLimit=_options.DifferentialBackupLimit
    };

    private T Read<T>(IpcEnvelope envelope)=>JsonSerializer.Deserialize<T>(envelope.PayloadJson,_json)??throw new InvalidOperationException($"Invalid payload for {envelope.Type}.");
    private object ToPortable(LudusaviCommandResult result)=>new{result.Success,result.ErrorCode,result.ErrorMessage,result.ExitCode,json=result.Json?.GetRawText(),result.WarningText};
    private IpcEnvelope Success(IpcEnvelope request,object payload)=>new(){RequestId=request.RequestId,Type=request.Type,IsResponse=true,Success=true,PayloadJson=JsonSerializer.Serialize(payload,_json)};
    private static IpcEnvelope Error(IpcEnvelope request,string code,string message)=>new(){RequestId=request.RequestId,Type=request.Type,IsResponse=true,Success=false,ErrorCode=code,ErrorMessage=message,PayloadJson="{}"};
}

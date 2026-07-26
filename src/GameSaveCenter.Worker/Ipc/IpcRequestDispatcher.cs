using System.Text.Json;
using GameSaveCenter.Contracts;
using GameSaveCenter.Worker.Configuration;
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
    private readonly ILogger<IpcRequestDispatcher> _logger;

    public IpcRequestDispatcher(GameCatalogService catalog,GameSessionCoordinator sessions,BackupOrchestrator backup,RestoreOrchestrator restore,
        MediaSyncService media,SavePathDetectionService detection,DashboardService dashboard,SqliteStateStore store,TaskCoordinator tasks,
        LudusaviClient ludusavi,WorkerOptions options,ILogger<IpcRequestDispatcher> logger)
    { _catalog=catalog;_sessions=sessions;_backup=backup;_restore=restore;_media=media;_detection=detection;_dashboard=dashboard;_store=store;_tasks=tasks;_ludusavi=ludusavi;_options=options;_logger=logger; }

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
                MessageTypes.UpdateBackupMetadata=>await UpdateMetadataAsync(Read<BackupMetadataUpdateDto>(request),token).ConfigureAwait(false),
                MessageTypes.RestorePreview=>ToPortable(await _restore.PreviewAsync(Read<RestoreRequestDto>(request),token).ConfigureAwait(false)),
                MessageTypes.RestoreExecute=>await _restore.ExecuteAsync(Read<RestoreRequestDto>(request),token).ConfigureAwait(false),
                MessageTypes.UndoRestore=>await _restore.UndoAsync(Read<GameQueryDto>(request).PlayniteId,token).ConfigureAwait(false),
                MessageTypes.SyncMedia=>await _media.SyncAsync(Read<MediaSyncRequestDto>(request),token).ConfigureAwait(false),
                MessageTypes.ListMedia=>await ListMediaAsync(Read<GameQueryDto>(request),token).ConfigureAwait(false),
                MessageTypes.ReassignMedia=>await ReassignMediaAsync(Read<ReassignMediaRequestDto>(request),token).ConfigureAwait(false),
                MessageTypes.DetectSavePaths=>await _detection.DetectAsync(Read<DetectionRequestDto>(request),token).ConfigureAwait(false),
                MessageTypes.AcceptSavePath=>await _detection.AcceptAsync(Read<AcceptSavePathRequestDto>(request),token).ConfigureAwait(false),
                MessageTypes.ValidateGame=>await ValidateAsync(Read<ValidateGameRequestDto>(request),token).ConfigureAwait(false),
                MessageTypes.GetTasks=>await _store.GetRecentTasksAsync(200,token).ConfigureAwait(false),
                MessageTypes.GetLogs=>await _store.GetAuditAsync(500,token).ConfigureAwait(false),
                MessageTypes.GetSettings=>SanitizedSettings(),
                MessageTypes.UpdateSettings=>UpdateSettings(Read<WorkerSettingsDto>(request)),
                MessageTypes.CancelTask=>new{cancelled=_tasks.Cancel(Read<CancelTaskRequestDto>(request).TaskId)},
                _=>throw new NotSupportedException($"Unknown IPC message type: {request.Type}")
            };
            return Success(request,payload);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex,"IPC request {Type} failed",request.Type);
            return Error(request,ex.GetType().Name,ex.Message);
        }
    }

    private async Task<object> UpsertAsync(List<GameDescriptorDto> games,CancellationToken token){await _catalog.UpsertAndMatchAsync(games,token).ConfigureAwait(false);return new{accepted=games.Count};}
    private async Task<object> StopAsync(GameSessionEventDto value,CancellationToken token){await _sessions.StopAsync(value,token).ConfigureAwait(false);return new{stopped=true};}
    private Task<List<BackupVersionDto>> ListBackupsAsync(GameQueryDto query,CancellationToken token)=>_store.GetBackupVersionsAsync(query.PlayniteId,token);
    private Task<List<MediaItemDto>> ListMediaAsync(GameQueryDto query,CancellationToken token)=>_store.GetMediaAsync(query.PlayniteId,query.Limit,token);

    private async Task<object> ReassignMediaAsync(ReassignMediaRequestDto request,CancellationToken token)
    {
        if(string.IsNullOrWhiteSpace(request.MediaId)||string.IsNullOrWhiteSpace(request.TargetPlayniteId))throw new InvalidOperationException("Media and target game are required.");
        await _store.ReassignMediaAsync(request.MediaId,request.TargetPlayniteId,token).ConfigureAwait(false);
        await _store.AppendAuditAsync("Media","Reassigned media",JsonSerializer.Serialize(request),token).ConfigureAwait(false);
        return new{updated=true};
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



    private object UpdateSettings(WorkerSettingsDto settings)
    {
        _options.Apply(settings);
        return SanitizedSettings();
    }

    private object SanitizedSettings()=>new
    {
        _options.DataDirectory,_options.LudusaviExecutable,_options.LudusaviBackupDirectory,_options.RcloneExecutable,
        RcloneDestination=string.IsNullOrWhiteSpace(_options.RcloneDestination)?string.Empty:"Configured",
        _options.MediaArchiveDirectory,_options.ProcessPollingSeconds,_options.DefaultBackupIntervalMinutes,_options.EnableProcessDetection,
        _options.EnableMediaSync,_options.EnableCloudUpload
    };

    private T Read<T>(IpcEnvelope envelope)=>JsonSerializer.Deserialize<T>(envelope.PayloadJson,_json)??throw new InvalidOperationException($"Invalid payload for {envelope.Type}.");
    private object ToPortable(LudusaviCommandResult result)=>new{result.Success,result.ErrorCode,result.ErrorMessage,result.ExitCode,json=result.Json?.GetRawText(),result.WarningText};
    private IpcEnvelope Success(IpcEnvelope request,object payload)=>new(){RequestId=request.RequestId,Type=request.Type,IsResponse=true,Success=true,PayloadJson=JsonSerializer.Serialize(payload,_json)};
    private static IpcEnvelope Error(IpcEnvelope request,string code,string message)=>new(){RequestId=request.RequestId,Type=request.Type,IsResponse=true,Success=false,ErrorCode=code,ErrorMessage=message,PayloadJson="{}"};
}

using System.Text.Json;
using GameSaveCenter.Contracts;
using GameSaveCenter.Worker.Infrastructure;
using GameSaveCenter.Worker.Persistence;

namespace GameSaveCenter.Worker.Services;

/// <summary>Implements an explicit, auditable restore state machine with a mandatory PreRestore backup.</summary>
public sealed class RestoreOrchestrator
{
    private readonly GameCatalogService _catalog;
    private readonly SqliteStateStore _store;
    private readonly LudusaviClient _ludusavi;
    private readonly TaskCoordinator _tasks;
    private readonly GameSessionCoordinator _sessions;
    private readonly CloudTransferCoordinator _cloudTransfers;

    public RestoreOrchestrator(GameCatalogService catalog,SqliteStateStore store,LudusaviClient ludusavi,TaskCoordinator tasks,GameSessionCoordinator sessions,CloudTransferCoordinator cloudTransfers)
    { _catalog=catalog;_store=store;_ludusavi=ludusavi;_tasks=tasks;_sessions=sessions;_cloudTransfers=cloudTransfers; }

    public async Task<LudusaviCommandResult> PreviewAsync(RestoreRequestDto request,CancellationToken token)
    {
        var match=await ResolveAsync(request.PlayniteId,token).ConfigureAwait(false);
        return await _ludusavi.RestoreAsync(match,request.BackupId,true,token).ConfigureAwait(false);
    }

    public async Task<TaskStatusDto> ExecuteAsync(RestoreRequestDto request,CancellationToken token)
    {
        if(!request.ConfirmedCurrentSnapshot||!request.ConfirmedGameClosed)
            throw new InvalidOperationException("Restore requires explicit confirmation that the game is closed and the current state may be snapshotted.");
        var game=await _catalog.GetGameAsync(request.PlayniteId,token).ConfigureAwait(false)??throw new InvalidOperationException("Game not found.");
        var match=await ResolveAsync(request.PlayniteId,token).ConfigureAwait(false);
        return await _tasks.RunAsync("Restore",game.PlayniteId,game.Name,async(progress,ct)=>
        {
            var state=RestoreState.Requested;
            await AuditAsync(game.PlayniteId,state,request,ct).ConfigureAwait(false);
            await progress.ReportAsync(5,"正在确认游戏已关闭").ConfigureAwait(false);
            await EnsureGameClosedAsync(game.PlayniteId,ct).ConfigureAwait(false);
            state=RestoreState.GameClosedVerified;await AuditAsync(game.PlayniteId,state,request,ct).ConfigureAwait(false);

            var before=await _ludusavi.ListBackupsAsync(new[]{match},ct).ConfigureAwait(false);
            var beforeIds=before.Success&&before.Json.HasValue?LudusaviResultParser.ParseBackupList(before.Json.Value,game.PlayniteId,match).Select(x=>x.BackupId).ToHashSet(StringComparer.OrdinalIgnoreCase):new HashSet<string>();
            await progress.ReportAsync(15,"正在创建 PreRestore 安全快照").ConfigureAwait(false);
            var pre=await _ludusavi.BackupAsync(new[]{match},true,false,ct).ConfigureAwait(false);
            if(!pre.Success||!pre.Json.HasValue||LudusaviResultParser.SomeGamesFailed(pre.Json.Value)) throw new InvalidOperationException("PreRestore backup failed; restore was not started.");
            var after=await _ludusavi.ListBackupsAsync(new[]{match},ct).ConfigureAwait(false);
            var versions=after.Success&&after.Json.HasValue?LudusaviResultParser.ParseBackupList(after.Json.Value,game.PlayniteId,match):new List<BackupVersionDto>();
            var preVersion=versions.FirstOrDefault(x=>!beforeIds.Contains(x.BackupId))??versions.OrderByDescending(x=>x.CreatedUtc).FirstOrDefault();
            if(preVersion==null) throw new InvalidOperationException("Could not identify the PreRestore backup version.");
            preVersion.IsPreRestore=true;preVersion.IsLocked=true;preVersion.Comment=$"PreRestore {DateTime.Now:yyyy-MM-dd HH:mm:ss} {request.UserComment}".Trim();
            var edit=await _ludusavi.EditBackupAsync(match,preVersion.BackupId,preVersion.Comment,true,ct).ConfigureAwait(false);
            if(!edit.Success) throw new InvalidOperationException("PreRestore was created, but it could not be locked in Ludusavi: "+edit.ErrorMessage);
            await _store.AddBackupVersionAsync(preVersion,"{}",ct).ConfigureAwait(false);
            state=RestoreState.PreRestoreBackupCreated;await AuditAsync(game.PlayniteId,state,new{request,preVersion.BackupId},ct).ConfigureAwait(false);

            await progress.ReportAsync(40,"正在预览目标版本").ConfigureAwait(false);
            var preview=await _ludusavi.RestoreAsync(match,request.BackupId,true,ct).ConfigureAwait(false);
            if(!preview.Success||!preview.Json.HasValue||LudusaviResultParser.SomeGamesFailed(preview.Json.Value)) throw new InvalidOperationException("Restore preview failed; live files were not changed.");

            await progress.ReportAsync(55,"正在等待现有云端传输安全结束").ConfigureAwait(false);
            using var cloudPause=await _cloudTransfers.PauseForRestoreAsync(ct).ConfigureAwait(false);
            state=RestoreState.CloudJobsPaused;await AuditAsync(game.PlayniteId,state,request,ct).ConfigureAwait(false);
            await progress.ReportAsync(60,"正在恢复指定版本").ConfigureAwait(false);
            var restored=await _ludusavi.RestoreAsync(match,request.BackupId,false,ct).ConfigureAwait(false);
            if(!restored.Success||!restored.Json.HasValue||LudusaviResultParser.SomeGamesFailed(restored.Json.Value))
            {
                state=RestoreState.RollbackAttempted;await AuditAsync(game.PlayniteId,state,new{request,preVersion.BackupId},ct).ConfigureAwait(false);
                var rollback=await _ludusavi.RestoreAsync(match,preVersion.BackupId,false,ct).ConfigureAwait(false);
                state=rollback.Success?RestoreState.RolledBack:RestoreState.ManualInterventionRequired;
                await AuditAsync(game.PlayniteId,state,new{request,preVersion.BackupId,rollback.ErrorMessage},ct).ConfigureAwait(false);
                throw new InvalidOperationException(rollback.Success?"Restore failed and the PreRestore snapshot was restored.":"Restore and automatic rollback both failed. Manual intervention is required.");
            }
            state=RestoreState.RestoreExecuted;await AuditAsync(game.PlayniteId,state,request,ct).ConfigureAwait(false);
            await progress.ReportAsync(88,"正在执行恢复后校验").ConfigureAwait(false);
            var post=await _ludusavi.RestoreAsync(match,request.BackupId,true,ct).ConfigureAwait(false);
            if(!post.Success||!post.Json.HasValue||LudusaviResultParser.SomeGamesFailed(post.Json.Value)) throw new InvalidOperationException("Restore completed but post-restore validation was inconclusive. PreRestore remains available.");
            state=RestoreState.PostRestoreValidated;await AuditAsync(game.PlayniteId,state,request,ct).ConfigureAwait(false);
            state=RestoreState.Completed;await AuditAsync(game.PlayniteId,state,new{request,preVersion.BackupId},ct).ConfigureAwait(false);
            await progress.ReportAsync(100,"安全恢复完成").ConfigureAwait(false);
        },token).ConfigureAwait(false);
    }

    public async Task<TaskStatusDto> UndoAsync(string playniteId,CancellationToken token)
    {
        var version=(await _store.GetBackupVersionsAsync(playniteId,token).ConfigureAwait(false)).FirstOrDefault(x=>x.IsPreRestore)
            ??throw new InvalidOperationException("No PreRestore snapshot is indexed for this game.");
        return await ExecuteAsync(new RestoreRequestDto{PlayniteId=playniteId,BackupId=version.BackupId,ConfirmedCurrentSnapshot=true,ConfirmedGameClosed=true,UserComment="Undo previous restore"},token).ConfigureAwait(false);
    }

    private async Task<string> ResolveAsync(string playniteId,CancellationToken token)
    {
        var matches=await _catalog.GetMatchesAsync(token).ConfigureAwait(false);
        return matches.TryGetValue(playniteId,out var match)&&!string.IsNullOrWhiteSpace(match.Name)?match.Name:throw new InvalidOperationException("Game is not matched to Ludusavi.");
    }

    private async Task EnsureGameClosedAsync(string playniteId,CancellationToken token)
    {
        if(_sessions.ActiveSessions.Any(x=>string.Equals(x.PlayniteId,playniteId,StringComparison.OrdinalIgnoreCase)))
            throw new WorkerOperationException("RESTORE_GAME_RUNNING","Worker 仍检测到该游戏会话正在运行，已阻止恢复。请退出游戏并稍后重试。",playniteId);

        var persisted=await _store.GetOpenSessionsAsync(token).ConfigureAwait(false);
        foreach(var session in persisted.Where(x=>string.Equals(x.PlayniteId,playniteId,StringComparison.OrdinalIgnoreCase)))
        {
            if(session.ProcessId is not int processId)continue;
            try
            {
                using var process=System.Diagnostics.Process.GetProcessById(processId);
                if(!process.HasExited)
                    throw new WorkerOperationException("RESTORE_GAME_PROCESS_RUNNING","Worker 仍检测到该游戏的已记录进程，已阻止恢复。请退出游戏、启动器及相关 MOD 工具后重试。",$"PID={processId}; Name={process.ProcessName}");
            }
            catch(ArgumentException)
            {
                // A stale persisted session cannot prove that a game is still running. The explicit
                // confirmation remains the safe fallback if no live process is known to the Worker.
            }
        }
    }

    private Task AuditAsync(string gameId,RestoreState state,object detail,CancellationToken token)=>_store.AppendAuditAsync("Restore",state.ToString(),JsonSerializer.Serialize(new{gameId,state,detail}),token);
}

using GameSaveCenter.Contracts;
using GameSaveCenter.Worker.Infrastructure;
using GameSaveCenter.Worker.Persistence;
using GameSaveCenter.Worker.Configuration;

namespace GameSaveCenter.Worker.Services;

/// <summary>Builds one dashboard snapshot so the Playnite UI can refresh atomically.</summary>
public sealed class DashboardService
{
    private readonly SqliteStateStore _store;
    private readonly GameCatalogService _catalog;
    private readonly GameSessionCoordinator _sessions;
    private readonly LudusaviClient _ludusavi;
    private readonly RcloneClient _rclone;
    private readonly WorkerOptions _options;

    public DashboardService(SqliteStateStore store,GameCatalogService catalog,GameSessionCoordinator sessions,LudusaviClient ludusavi,RcloneClient rclone,WorkerOptions options)
    { _store=store;_catalog=catalog;_sessions=sessions;_ludusavi=ludusavi;_rclone=rclone;_options=options; }

    public async Task<DashboardSnapshotDto> GetAsync(CancellationToken token)
    {
        var games=await _catalog.GetGamesAsync(token).ConfigureAwait(false);
        var matches=await _catalog.GetMatchesAsync(token).ConfigureAwait(false);
        var active=_sessions.ActiveSessions.Select(x=>x.PlayniteId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tasks=await _store.GetRecentTasksAsync(50,token).ConfigureAwait(false);
        var findings=await _store.GetOpenFindingsAsync(100,token).ConfigureAwait(false);
        var counts=await _store.GetCountsAsync(token).ConfigureAwait(false);
        var audit=await _store.GetAuditAsync(100,token).ConfigureAwait(false);
        var ludusaviVersion = _ludusavi.IsAvailable ? await _ludusavi.GetVersionAsync(token).ConfigureAwait(false) : string.Empty;
        var snapshot=new DashboardSnapshotDto
        {
            GeneratedUtc=DateTime.UtcNow,WorkerHealthy=true,WorkerVersion=typeof(DashboardService).Assembly.GetName().Version?.ToString()??"dev",
            LudusaviAvailable=_ludusavi.IsAvailable,RcloneAvailable=_rclone.IsAvailable,LudusaviVersion=ludusaviVersion,
            LudusaviExecutable=_options.LudusaviExecutable,LudusaviBackupDirectory=_options.LudusaviBackupDirectory,BackupFormat=_options.BackupFormat,
            ManagedGames=counts.Games,MatchedGames=counts.Matched,
            RunningGames=active.Count,WarningGames=findings.Where(x=>x.Severity>=FindingSeverity.Warning).Select(x=>x.PlayniteId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),PendingCloudTasks=tasks.Count(x=>x.TaskType.Contains("Cloud",StringComparison.OrdinalIgnoreCase)&&x.State is TaskState.Queued or TaskState.Running),
            UnassignedMediaCount=counts.Unassigned,RecentTasks=tasks,Findings=findings,RecentAudit=audit
        };
        foreach(var game in games)
        {
            var versions=await _store.GetBackupVersionsAsync(game.PlayniteId,token).ConfigureAwait(false);
            var media=await _store.GetMediaAsync(game.PlayniteId,5000,token).ConfigureAwait(false);
            var matched=matches.TryGetValue(game.PlayniteId,out var match)&&!string.IsNullOrWhiteSpace(match.Name);
            snapshot.Games.Add(new GameStatusDto
            {
                PlayniteId=game.PlayniteId,Name=game.Name,Platform=game.Platform,IsRunning=active.Contains(game.PlayniteId),LudusaviMatched=matched,
                LudusaviName=matched?match.Name:string.Empty,LastBackupUtc=versions.FirstOrDefault()?.CreatedUtc,BackupVersionCount=versions.Count,
                LastMediaSyncUtc=media.FirstOrDefault()?.CapturedUtc,MediaCount=media.Count,CloudState=_rclone.IsConfigured?"Configured":"Disabled",
                HealthState=!_ludusavi.IsAvailable?"LudusaviUnavailable":findings.Any(x=>string.Equals(x.PlayniteId,game.PlayniteId,StringComparison.OrdinalIgnoreCase)&&x.Severity>=FindingSeverity.Error)?"Attention":matched?"Ready":"Unmatched",
                Policy=await _store.GetPolicyAsync(game.PlayniteId,token).ConfigureAwait(false)
            });
        }
        return snapshot;
    }
}

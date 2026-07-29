using GameSaveCenter.Contracts;
using GameSaveCenter.Worker.Infrastructure;
using GameSaveCenter.Worker.Persistence;
using GameSaveCenter.Worker.Configuration;

namespace GameSaveCenter.Worker.Services;

/// <summary>Builds one dashboard snapshot so the Playnite UI can refresh atomically.</summary>
public sealed class DashboardService
{
    private readonly SqliteStateStore _store;
    private readonly GameSessionCoordinator _sessions;
    private readonly LudusaviClient _ludusavi;
    private readonly RcloneClient _rclone;
    private readonly WorkerOptions _options;
    private readonly SemaphoreSlim _versionGate=new(1,1);
    private string _cachedLudusaviVersion=string.Empty;
    private DateTime _versionCachedUtc=DateTime.MinValue;

    public DashboardService(SqliteStateStore store,GameSessionCoordinator sessions,LudusaviClient ludusavi,RcloneClient rclone,WorkerOptions options)
    { _store=store;_sessions=sessions;_ludusavi=ludusavi;_rclone=rclone;_options=options; }

    public async Task<DashboardSnapshotDto> GetAsync(CancellationToken token)
    {
        var games=await _store.GetDashboardGameRecordsAsync(token).ConfigureAwait(false);
        var active=_sessions.ActiveSessions.Select(x=>x.PlayniteId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tasks=await _store.GetRecentTasksAsync(50,token).ConfigureAwait(false);
        var findings=await _store.GetOpenFindingsAsync(100,token).ConfigureAwait(false);
        var counts=await _store.GetCountsAsync(token).ConfigureAwait(false);
        var audit=await _store.GetAuditAsync(100,token).ConfigureAwait(false);
        var ludusaviVersion = await GetLudusaviVersionAsync(token).ConfigureAwait(false);
        var attentionGames=findings
            .Where(x=>x.Severity>=FindingSeverity.Error)
            .Select(x=>x.PlayniteId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var gameNames=games.ToDictionary(x=>x.Descriptor.PlayniteId,x=>x.Descriptor.Name,StringComparer.OrdinalIgnoreCase);
        foreach(var finding in findings)
        {
            if(!string.IsNullOrWhiteSpace(finding.PlayniteId) && gameNames.TryGetValue(finding.PlayniteId,out var gameName))
                finding.GameName=gameName;
            else if(string.IsNullOrWhiteSpace(finding.GameName))
                finding.GameName="全局";
        }
        var snapshot=new DashboardSnapshotDto
        {
            GeneratedUtc=DateTime.UtcNow,WorkerHealthy=true,WorkerVersion=typeof(DashboardService).Assembly.GetName().Version?.ToString()??"dev",
            LudusaviAvailable=_ludusavi.IsAvailable,RcloneAvailable=_rclone.IsAvailable,LudusaviVersion=ludusaviVersion,
            LudusaviExecutable=_options.LudusaviExecutable,LudusaviBackupDirectory=_options.LudusaviBackupDirectory,BackupFormat=_options.BackupFormat,
            ManagedGames=counts.Games,MatchedGames=counts.Matched,
            RunningGames=active.Count,WarningGames=findings.Where(x=>x.Severity>=FindingSeverity.Warning).Select(x=>x.PlayniteId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),PendingCloudTasks=tasks.Count(x=>x.TaskType.Contains("Cloud",StringComparison.OrdinalIgnoreCase)&&x.State is TaskState.Queued or TaskState.Running),
            UnassignedMediaCount=counts.Unassigned,RecentTasks=tasks,Findings=findings,RecentAudit=audit
        };
        foreach(var record in games)
        {
            var game=record.Descriptor;
            var matched=!string.IsNullOrWhiteSpace(record.LudusaviName);
            snapshot.Games.Add(new GameStatusDto
            {
                PlayniteId=game.PlayniteId,Name=game.Name,Platform=game.Platform,IsRunning=active.Contains(game.PlayniteId),LudusaviMatched=matched,
                LudusaviName=record.LudusaviName,LastBackupUtc=record.LastBackupUtc,BackupVersionCount=record.BackupVersionCount,
                LastMediaSyncUtc=record.LastMediaUtc,MediaCount=record.MediaCount,CloudState=_rclone.IsConfigured?"Configured":"Disabled",
                HealthState=!_ludusavi.IsAvailable?"LudusaviUnavailable":attentionGames.Contains(game.PlayniteId)?"Attention":matched?"Ready":"Unmatched",
                Policy=record.Policy
            });
        }
        return snapshot;
    }

    private async Task<string> GetLudusaviVersionAsync(CancellationToken token)
    {
        if(!_ludusavi.IsAvailable) return string.Empty;
        if(DateTime.UtcNow-_versionCachedUtc<TimeSpan.FromHours(6)) return _cachedLudusaviVersion;
        await _versionGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if(DateTime.UtcNow-_versionCachedUtc<TimeSpan.FromHours(6)) return _cachedLudusaviVersion;
            _cachedLudusaviVersion=await _ludusavi.GetVersionAsync(token).ConfigureAwait(false);
            _versionCachedUtc=DateTime.UtcNow;
            return _cachedLudusaviVersion;
        }
        finally
        {
            _versionGate.Release();
        }
    }
}

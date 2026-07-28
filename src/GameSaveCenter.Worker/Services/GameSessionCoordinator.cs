using System.Collections.Concurrent;
using GameSaveCenter.Contracts;
using GameSaveCenter.Worker.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Services;

/// <summary>Combines Playnite events and process detection into one deduplicated game session.</summary>
public sealed class GameSessionCoordinator : BackgroundService
{
    private readonly SqliteStateStore _store;
    private readonly BackupOrchestrator _backup;
    private readonly MediaSyncService _media;
    private readonly SavePathDetectionService _detection;
    private readonly GameToolService _gameTools;
    private readonly ILogger<GameSessionCoordinator> _logger;
    private readonly ConcurrentDictionary<string,ActiveSession> _active=new(StringComparer.OrdinalIgnoreCase);

    public GameSessionCoordinator(SqliteStateStore store,BackupOrchestrator backup,MediaSyncService media,SavePathDetectionService detection,GameToolService gameTools,ILogger<GameSessionCoordinator> logger)
    { _store=store;_backup=backup;_media=media;_detection=detection;_gameTools=gameTools;_logger=logger; }

    public IReadOnlyCollection<GameSessionEventDto> ActiveSessions=>_active.Values.Select(x=>x.Event).ToList();

    public async Task<GameSessionEventDto> StartAsync(GameSessionEventDto incoming,CancellationToken token)
    {
        if(string.IsNullOrWhiteSpace(incoming.PlayniteId))throw new ArgumentException("PlayniteId is required.");
        if(_active.TryGetValue(incoming.PlayniteId,out var existing))
        {
            // Prefer the precise Playnite event, but preserve one logical session.
            if(incoming.Source==SessionSourceKind.Playnite) existing.Event.Source=SessionSourceKind.Playnite;
            if(incoming.ProcessId.HasValue)existing.Event.ProcessId=incoming.ProcessId;
            if(!string.IsNullOrWhiteSpace(incoming.ProcessName))existing.Event.ProcessName=incoming.ProcessName;
            await _store.AddSessionAsync(existing.Event,token).ConfigureAwait(false);return existing.Event;
        }
        incoming.SessionId=string.IsNullOrWhiteSpace(incoming.SessionId)?Guid.NewGuid().ToString("N"):incoming.SessionId;
        incoming.StartedUtc=incoming.StartedUtc==default?DateTime.UtcNow:incoming.StartedUtc.ToUniversalTime();
        var policy=await _store.GetPolicyAsync(incoming.PlayniteId,token).ConfigureAwait(false);
        var intervalMinutes = Math.Max(1, policy.DuringPlayIntervalMinutes);
        var timedBackupEnabled=policy.Enabled&&policy.BackupDuringPlay;
        var active=new ActiveSession(incoming,DateTime.UtcNow.AddMinutes(intervalMinutes),intervalMinutes,timedBackupEnabled);
        _active[incoming.PlayniteId]=active;await _store.AddSessionAsync(incoming,token).ConfigureAwait(false);
        _detection.BeginSessionCapture(incoming);
        _=RunSafeAsync(()=>_gameTools.StartAutomaticAsync(incoming,CancellationToken.None),"automatic game tools",incoming.GameName);
        _logger.LogInformation("Session started for {Game} from {Source}; timed backup is scheduled every {IntervalMinutes} minute(s)",incoming.GameName,incoming.Source,intervalMinutes);return incoming;
    }

    public async Task StopAsync(GameSessionEventDto incoming,CancellationToken token)
    {
        if(!_active.TryRemove(incoming.PlayniteId,out var active))return;
        active.Event.StoppedUtc=incoming.StoppedUtc??DateTime.UtcNow;
        active.Event.ElapsedSeconds=incoming.ElapsedSeconds>0?incoming.ElapsedSeconds:(long)(active.Event.StoppedUtc.Value-active.Event.StartedUtc).TotalSeconds;
        await _store.AddSessionAsync(active.Event,token).ConfigureAwait(false);
        await _gameTools.StopAutomaticAsync(active.Event.SessionId,token).ConfigureAwait(false);
        var policy=await _store.GetPolicyAsync(active.Event.PlayniteId,token).ConfigureAwait(false);
        if(policy.Enabled&&policy.BackupOnGameStop)
            _=RunSafeAsync(()=>_backup.BackupAsync(new BackupRequestDto{PlayniteIds=new(){active.Event.PlayniteId},Force=true,Reason="GameStopped",SessionId=active.Event.SessionId},CancellationToken.None),"exit backup",active.Event.GameName);
        if(policy.Enabled&&policy.SyncMediaOnGameStop)
            _=RunSafeAsync(()=>_media.SyncAsync(new MediaSyncRequestDto{PlayniteIds=new(){active.Event.PlayniteId},SessionId=active.Event.SessionId,UploadAfterSync=policy.UploadAfterBackup},CancellationToken.None),"exit media sync",active.Event.GameName);
        _=RunSafeAsync(()=>_detection.AnalyzeSessionStopAsync(active.Event,CancellationToken.None),"session save-path analysis",active.Event.GameName);
        _logger.LogInformation("Session stopped for {Game}",active.Event.GameName);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            foreach(var pair in _active.ToArray())
            {
                var policy=await _store.GetPolicyAsync(pair.Key,stoppingToken).ConfigureAwait(false);
                var intervalMinutes = Math.Max(1, policy.DuringPlayIntervalMinutes);
                var timedBackupEnabled=policy.Enabled&&policy.BackupDuringPlay;
                if(pair.Value.IntervalMinutes!=intervalMinutes)
                {
                    pair.Value.IntervalMinutes=intervalMinutes;
                    pair.Value.NextBackupUtc=DateTime.UtcNow.AddMinutes(intervalMinutes);
                    _logger.LogInformation("Timed backup schedule changed for {Game}; next run is in {IntervalMinutes} minute(s)",pair.Value.Event.GameName,intervalMinutes);
                }
                if(pair.Value.TimedBackupEnabled!=timedBackupEnabled)
                {
                    pair.Value.TimedBackupEnabled=timedBackupEnabled;
                    if(timedBackupEnabled)pair.Value.NextBackupUtc=DateTime.UtcNow.AddMinutes(intervalMinutes);
                    _logger.LogInformation("Timed backup schedule {State} for {Game}; next UTC {NextUtc}",timedBackupEnabled?"enabled":"disabled",pair.Value.Event.GameName,timedBackupEnabled?pair.Value.NextBackupUtc:null);
                }
                if(!timedBackupEnabled||DateTime.UtcNow<pair.Value.NextBackupUtc)continue;
                var scheduledUtc=pair.Value.NextBackupUtc;
                do pair.Value.NextBackupUtc=pair.Value.NextBackupUtc.AddMinutes(intervalMinutes);
                while(pair.Value.NextBackupUtc<=DateTime.UtcNow);
                _logger.LogInformation("Timed backup cadence reached for {Game}; scheduled UTC {ScheduledUtc:o}, next UTC {NextUtc:o}",pair.Value.Event.GameName,scheduledUtc,pair.Value.NextBackupUtc);
                if(Interlocked.CompareExchange(ref pair.Value.BackupPending,1,0)==0)
                {
                    _logger.LogInformation("Starting timed backup for {Game}",pair.Value.Event.GameName);
                    _=RunTimedBackupAsync(pair.Key,pair.Value);
                }
                else
                    _logger.LogWarning("Skipped overlapping timed backup for {Game}; the previous backup is still pending",pair.Value.Event.GameName);
                if(policy.SyncMediaDuringPlay)
                    _=RunSafeAsync(()=>_media.SyncAsync(new MediaSyncRequestDto{PlayniteIds=new(){pair.Key},SessionId=pair.Value.Event.SessionId,UploadAfterSync=false},CancellationToken.None),"timed media sync",pair.Value.Event.GameName);
            }
            // Keep a one-minute policy reasonably precise without running any file work on this loop.
            await Task.Delay(TimeSpan.FromSeconds(5),stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunSafeAsync(Func<Task> operation,string label,string game)
    {
        try{await operation().ConfigureAwait(false);}catch(Exception ex){_logger.LogError(ex,"{Label} failed for {Game}",label,game);}
    }

    private async Task RunTimedBackupAsync(string playniteId,ActiveSession active)
    {
        try
        {
            await RunSafeAsync(()=>_backup.BackupAsync(new BackupRequestDto
            {
                PlayniteIds=new(){playniteId},Force=true,Reason="DuringPlay",SessionId=active.Event.SessionId
            },CancellationToken.None),"timed backup",active.Event.GameName).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref active.BackupPending,0);
        }
    }

    private sealed class ActiveSession
    {
        public ActiveSession(GameSessionEventDto @event,DateTime nextBackupUtc,int intervalMinutes,bool timedBackupEnabled){Event=@event;NextBackupUtc=nextBackupUtc;IntervalMinutes=intervalMinutes;TimedBackupEnabled=timedBackupEnabled;}
        public GameSessionEventDto Event{get;}
        public DateTime NextBackupUtc{get;set;}
        public int IntervalMinutes{get;set;}
        public bool TimedBackupEnabled{get;set;}
        public int BackupPending;
    }
}

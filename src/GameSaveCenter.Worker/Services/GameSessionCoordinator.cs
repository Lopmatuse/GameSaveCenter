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
        var active=new ActiveSession(incoming,DateTime.UtcNow.AddMinutes(Math.Max(5,policy.DuringPlayIntervalMinutes)));
        _active[incoming.PlayniteId]=active;await _store.AddSessionAsync(incoming,token).ConfigureAwait(false);
        _detection.BeginSessionCapture(incoming);
        _=RunSafeAsync(()=>_gameTools.StartAutomaticAsync(incoming,CancellationToken.None),"automatic game tools",incoming.GameName);
        _logger.LogInformation("Session started for {Game} from {Source}",incoming.GameName,incoming.Source);return incoming;
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
                if(!policy.Enabled||!policy.BackupDuringPlay||DateTime.UtcNow<pair.Value.NextBackupUtc)continue;
                pair.Value.NextBackupUtc=DateTime.UtcNow.AddMinutes(Math.Max(5,policy.DuringPlayIntervalMinutes));
                _=RunSafeAsync(()=>_backup.BackupAsync(new BackupRequestDto{PlayniteIds=new(){pair.Key},Force=true,Reason="DuringPlay",SessionId=pair.Value.Event.SessionId},CancellationToken.None),"timed backup",pair.Value.Event.GameName);
                if(policy.SyncMediaDuringPlay)
                    _=RunSafeAsync(()=>_media.SyncAsync(new MediaSyncRequestDto{PlayniteIds=new(){pair.Key},SessionId=pair.Value.Event.SessionId,UploadAfterSync=false},CancellationToken.None),"timed media sync",pair.Value.Event.GameName);
            }
            await Task.Delay(TimeSpan.FromSeconds(20),stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunSafeAsync(Func<Task> operation,string label,string game)
    {
        try{await operation().ConfigureAwait(false);}catch(Exception ex){_logger.LogError(ex,"{Label} failed for {Game}",label,game);}
    }

    private sealed class ActiveSession
    {
        public ActiveSession(GameSessionEventDto @event,DateTime nextBackupUtc){Event=@event;NextBackupUtc=nextBackupUtc;}
        public GameSessionEventDto Event{get;}
        public DateTime NextBackupUtc{get;set;}
    }
}

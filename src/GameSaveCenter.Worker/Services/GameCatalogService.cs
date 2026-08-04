using System.Text.Json;
using GameSaveCenter.Contracts;
using GameSaveCenter.Worker.Infrastructure;
using GameSaveCenter.Worker.Persistence;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Services;

/// <summary>Maintains Playnite descriptors and resolves them to Ludusavi manifest titles.</summary>
public sealed class GameCatalogService
{
    // A full Playnite library can contain hundreds or thousands of entries. Matching every
    // changed descriptor synchronously inside the IPC request makes the Worker look dead to
    // Playnite while Ludusavi is being started once per game. Keep the durable descriptor
    // update synchronous, but let large refreshes drain in the background.
    private const int BackgroundMatchThreshold = 20;
    private const int BackgroundMatchBatchSize = 4;
    // A large Playnite library often contains hundreds of uninstalled entries.  They cannot
    // produce a save backup until they are installed, so matching all of them on the first
    // dashboard open only creates a long stream of short-lived Ludusavi processes.  Keep each
    // catalog refresh bounded to a useful foreground set; the next library refresh or the
    // game's own start event can enqueue the remaining entries later.
    private const int LargeLibraryBackgroundMatchBudget = 64;
    private static readonly TimeSpan RecentlyPlayedPriorityWindow = TimeSpan.FromDays(90);
    private static readonly TimeSpan BackgroundMatchYieldDelay = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan BackgroundMatchInitialDelay = TimeSpan.FromSeconds(30);
    private readonly SqliteStateStore _store;
    private readonly LudusaviClient _ludusavi;
    private readonly ILogger<GameCatalogService> _logger;
    private readonly object _backgroundMatchGate = new();
    private readonly Dictionary<string, PendingMatch> _backgroundMatches = new(StringComparer.OrdinalIgnoreCase);
    private Task? _backgroundMatchTask;
    private DateTime _backgroundMatchNotBeforeUtc = DateTime.MinValue;

    public GameCatalogService(SqliteStateStore store,LudusaviClient ludusavi,ILogger<GameCatalogService> logger)
    { _store=store;_ludusavi=ludusavi;_logger=logger; }

    public async Task UpsertAndMatchAsync(IEnumerable<GameDescriptorDto> games,CancellationToken token)
    {
        var list=games.Where(x=>!string.IsNullOrWhiteSpace(x.PlayniteId)).ToList();
        var cached=await _store.GetGameMatchCacheAsync(token).ConfigureAwait(false);
        var now=DateTime.UtcNow;
        var retryBefore=now.AddDays(-7);
        var pending=new List<(GameDescriptorDto Game,string InputHash)>();
        var changedDescriptors=new List<GameDescriptorDto>();
        foreach(var game in list)
        {
            var inputHash=GameMatchInput.CreateHash(game);
            if(!cached.TryGetValue(game.PlayniteId,out var previous))
            {
                pending.Add((game,inputHash));
                changedDescriptors.Add(game);
                continue;
            }

            var previousHash=string.IsNullOrWhiteSpace(previous.MatchInputHash)
                ? GameMatchInput.CreateHash(previous.Descriptor)
                : previous.MatchInputHash;
            var inputChanged=!string.Equals(previousHash,inputHash,StringComparison.Ordinal);
            // A large refresh queues matching outside the IPC request. If the Worker exits
            // between persisting the descriptor and executing that queued item, the durable
            // row has the new input hash but no match-attempt timestamp. Treat that state as
            // pending on the next refresh so a restart cannot strand an unmatched game forever.
            var matchWasNeverAttempted=string.IsNullOrWhiteSpace(previous.LudusaviName)
                                       && !previous.LastMatchAttemptUtc.HasValue;
            var unmatchedRetryDue=string.IsNullOrWhiteSpace(previous.LudusaviName)
                                  && previous.LastMatchAttemptUtc.HasValue
                                  && previous.LastMatchAttemptUtc.Value<=retryBefore;
            // An unavailable Ludusavi must not make every restart look like a changed game.
            // Keep a never-matched descriptor durable, but retry it only once the executable
            // is actually configured and can service the queued lookup.
            var retryMatch = _ludusavi.IsAvailable && (matchWasNeverAttempted || unmatchedRetryDue);
            if(inputChanged||retryMatch)
            {
                pending.Add((game,inputHash));
                changedDescriptors.Add(game);
            }
        }

        // The Playnite host can raise a full-library synchronization after every restart.
        // The durable match-input hash is the cache contract: when nothing changed, avoid
        // rewriting hundreds or thousands of descriptor rows and updating their timestamps.
        // When a subset changed, persist only that subset; unchanged rows remain durable and
        // their backup/media/policy history is untouched. This keeps a 900+ game profile from
        // turning a harmless refresh into a long SQLite write transaction.
        if(changedDescriptors.Count>0)
            await _store.UpsertGamesAsync(changedDescriptors,token).ConfigureAwait(false);
        else
        {
            _logger.LogDebug("Skipped unchanged game descriptor persistence for {GameCount} games.", list.Count);
            return;
        }

        if(!_ludusavi.IsAvailable||pending.Count==0) return;
        _logger.LogInformation(
            "Ludusavi matching {PendingCount} changed or new games; {CachedCount} cached descriptors were reused.",
            pending.Count,
            list.Count-pending.Count);
        // Never make a complete library refresh wait for one Ludusavi process per game. A
        // single-game update (for example, a game that is starting now) remains synchronous so
        // its session has a match available, while large library refreshes return immediately.
        if (pending.Count >= BackgroundMatchThreshold || list.Count >= 100)
        {
            var backgroundPending = list.Count >= 100
                ? pending
                    .Where(x => x.Game.IsInstalled || IsRecentlyPlayed(x.Game, now))
                    .OrderByDescending(x => x.Game.IsInstalled)
                    .ThenByDescending(x => x.Game.LastPlayedUtc ?? DateTime.MinValue)
                    .ThenBy(x => x.Game.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(LargeLibraryBackgroundMatchBudget)
                    .ToList()
                : pending;
            QueueBackgroundMatches(backgroundPending);
            _logger.LogInformation(
                "Library descriptors persisted; {QueuedCount} Ludusavi matches queued in the background ({DeferredCount} low-priority entries deferred).",
                backgroundPending.Count,
                Math.Max(0, pending.Count - backgroundPending.Count));
            return;
        }

        foreach(var item in pending) await MatchOneAsync(new PendingMatch(item.Game, item.InputHash),token).ConfigureAwait(false);
    }

    private void QueueBackgroundMatches(IEnumerable<(GameDescriptorDto Game,string InputHash)> pending)
    {
        lock (_backgroundMatchGate)
        {
            foreach (var item in pending)
                _backgroundMatches[item.Game.PlayniteId] = new PendingMatch(item.Game, item.InputHash);

            // The descriptor rows are already durable and immediately usable by the UI.  Give
            // Playnite and the Worker a short quiet period before starting a large wave of
            // one-shot Ludusavi processes; otherwise a 900+ game import can make the host look
            // frozen even though the IPC request itself has already completed.
            if (_backgroundMatches.Count > 0 && _backgroundMatchNotBeforeUtc < DateTime.UtcNow)
                _backgroundMatchNotBeforeUtc = DateTime.UtcNow.Add(BackgroundMatchInitialDelay);

            if (_backgroundMatchTask == null || _backgroundMatchTask.IsCompleted)
                _backgroundMatchTask = Task.Run(ProcessBackgroundMatchesAsync);
        }
    }

    private async Task ProcessBackgroundMatchesAsync()
    {
        try
        {
            while (true)
            {
                TimeSpan wait;
                lock (_backgroundMatchGate)
                    wait = _backgroundMatchNotBeforeUtc - DateTime.UtcNow;
                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait).ConfigureAwait(false);

                List<PendingMatch> batch;
                lock (_backgroundMatchGate)
                {
                    if (_backgroundMatches.Count == 0)
                    {
                        _backgroundMatchTask = null;
                        return;
                    }

                    // Small, prioritized batches keep the Worker responsive to backup, task and
                    // UI requests while a very large library is being indexed. Installed and
                    // recently played games are useful first because they are the only entries
                    // that can immediately participate in a backup/session operation.
                    batch = _backgroundMatches.Values
                        .OrderBy(x => x.Game.IsInstalled ? 0 : 1)
                        .ThenByDescending(x => x.Game.LastPlayedUtc ?? DateTime.MinValue)
                        .ThenBy(x => x.Game.Name, StringComparer.OrdinalIgnoreCase)
                        .Take(BackgroundMatchBatchSize)
                        .ToList();
                    foreach (var item in batch) _backgroundMatches.Remove(item.Game.PlayniteId);
                    if (_backgroundMatches.Count == 0) _backgroundMatchNotBeforeUtc = DateTime.MinValue;
                }

                foreach (var item in batch)
                    await MatchOneAsync(item, CancellationToken.None).ConfigureAwait(false);
                // Do not let hundreds of short-lived Ludusavi processes monopolize the
                // machine. The pause is deliberately outside the store lock and does not
                // block IPC request handling; the next batch can resume after UI/backup work
                // has had an opportunity to run.
                await Task.Delay(BackgroundMatchYieldDelay).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            lock (_backgroundMatchGate) _backgroundMatchTask = null;
            _logger.LogError(ex, "Background Ludusavi matching stopped unexpectedly; cached descriptors remain available.");
        }
    }

    private async Task MatchOneAsync(PendingMatch item, CancellationToken token)
    {
        var game=item.Game;
        try
        {
            var result=await _ludusavi.FindAsync(game.Name,game.PlatformGameId,game.Platform==GamePlatformKind.Steam,game.Platform==GamePlatformKind.Gog,token).ConfigureAwait(false);
            if(!result.Success)
            {
                await _store.SetGameMatchAsync(game.PlayniteId,string.Empty,0,item.InputHash,token).ConfigureAwait(false);
                await _store.AppendAuditAsync("LudusaviMatch",$"匹配失败：{game.Name}",JsonSerializer.Serialize(new{result.ErrorCode,result.ErrorMessage,result.ExitCode,result.RawOutput}),token).ConfigureAwait(false);
                return;
            }
            var match=ExtractBestFindMatch(result.Json);
            await _store.SetGameMatchAsync(game.PlayniteId,match.Name,match.Score,item.InputHash,token).ConfigureAwait(false);
            if(match.Name.Length==0)
                await _store.AppendAuditAsync("LudusaviMatch",$"未找到匹配：{game.Name}",result.Json?.GetRawText()??"{}",token).ConfigureAwait(false);
        }
        catch(Exception ex)
        {
            _logger.LogWarning(ex,"Could not match {Game}",game.Name);
            await _store.SetGameMatchAsync(game.PlayniteId,string.Empty,0,item.InputHash,token).ConfigureAwait(false);
            await _store.AppendAuditAsync("LudusaviMatch",$"匹配异常：{game.Name}",JsonSerializer.Serialize(new{error=ex.Message}),token).ConfigureAwait(false);
        }
    }

    private sealed record PendingMatch(GameDescriptorDto Game,string InputHash);

    private static bool IsRecentlyPlayed(GameDescriptorDto game, DateTime now)
        => game.LastPlayedUtc.HasValue && now - game.LastPlayedUtc.Value.ToUniversalTime() <= RecentlyPlayedPriorityWindow;

    public Task<List<GameDescriptorDto>> GetGamesAsync(CancellationToken token)=>_store.GetGamesAsync(token);
    public Task<GameDescriptorDto?> GetGameAsync(string id,CancellationToken token)=>_store.GetGameAsync(id,token);
    public Task<Dictionary<string,(string Name,double Confidence)>> GetMatchesAsync(CancellationToken token)=>_store.GetGameMatchesAsync(token);

    private static (string Name,double Score) ExtractBestFindMatch(JsonElement? root)
    {
        if(root is not { ValueKind:JsonValueKind.Object } value || !value.TryGetProperty("games",out var games) || games.ValueKind!=JsonValueKind.Object)
            return (string.Empty,0);
        var best=(Name:string.Empty,Score:0d);
        foreach(var property in games.EnumerateObject())
        {
            var score=property.Value.ValueKind==JsonValueKind.Object && property.Value.TryGetProperty("score",out var scoreNode) && scoreNode.ValueKind==JsonValueKind.Number
                ? scoreNode.GetDouble():0.9;
            if(score>best.Score) best=(property.Name,score);
        }
        return best;
    }
}

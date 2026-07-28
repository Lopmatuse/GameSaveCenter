using System.Text.Json;
using GameSaveCenter.Contracts;
using GameSaveCenter.Worker.Infrastructure;
using GameSaveCenter.Worker.Persistence;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Services;

/// <summary>Maintains Playnite descriptors and resolves them to Ludusavi manifest titles.</summary>
public sealed class GameCatalogService
{
    private readonly SqliteStateStore _store;
    private readonly LudusaviClient _ludusavi;
    private readonly ILogger<GameCatalogService> _logger;

    public GameCatalogService(SqliteStateStore store,LudusaviClient ludusavi,ILogger<GameCatalogService> logger)
    { _store=store;_ludusavi=ludusavi;_logger=logger; }

    public async Task UpsertAndMatchAsync(IEnumerable<GameDescriptorDto> games,CancellationToken token)
    {
        var list=games.Where(x=>!string.IsNullOrWhiteSpace(x.PlayniteId)).ToList();
        var cached=await _store.GetGameMatchCacheAsync(token).ConfigureAwait(false);
        var now=DateTime.UtcNow;
        var retryBefore=now.AddDays(-7);
        var pending=new List<(GameDescriptorDto Game,string InputHash)>();
        foreach(var game in list)
        {
            var inputHash=GameMatchInput.CreateHash(game);
            if(!cached.TryGetValue(game.PlayniteId,out var previous))
            {
                pending.Add((game,inputHash));
                continue;
            }

            var previousHash=string.IsNullOrWhiteSpace(previous.MatchInputHash)
                ? GameMatchInput.CreateHash(previous.Descriptor)
                : previous.MatchInputHash;
            var inputChanged=!string.Equals(previousHash,inputHash,StringComparison.Ordinal);
            var unmatchedRetryDue=string.IsNullOrWhiteSpace(previous.LudusaviName)
                                  && previous.LastMatchAttemptUtc.HasValue
                                  && previous.LastMatchAttemptUtc.Value<=retryBefore;
            if(inputChanged||unmatchedRetryDue) pending.Add((game,inputHash));
        }

        await _store.UpsertGamesAsync(list,token).ConfigureAwait(false);
        if(!_ludusavi.IsAvailable||pending.Count==0) return;
        _logger.LogInformation(
            "Ludusavi matching {PendingCount} changed or new games; {CachedCount} cached descriptors were reused.",
            pending.Count,
            list.Count-pending.Count);
        foreach(var item in pending)
        {
            var game=item.Game;
            try
            {
                var result=await _ludusavi.FindAsync(game.Name,game.PlatformGameId,game.Platform==GamePlatformKind.Steam,game.Platform==GamePlatformKind.Gog,token).ConfigureAwait(false);
                if(!result.Success)
                {
                    await _store.SetGameMatchAsync(game.PlayniteId,string.Empty,0,item.InputHash,token).ConfigureAwait(false);
                    await _store.AppendAuditAsync("LudusaviMatch",$"匹配失败：{game.Name}",JsonSerializer.Serialize(new{result.ErrorCode,result.ErrorMessage,result.ExitCode,result.RawOutput}),token).ConfigureAwait(false);
                    continue;
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
    }

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

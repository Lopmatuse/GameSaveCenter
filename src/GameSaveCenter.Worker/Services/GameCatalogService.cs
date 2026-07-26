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
        await _store.UpsertGamesAsync(list,token).ConfigureAwait(false);
        if(!_ludusavi.IsAvailable) return;
        foreach(var game in list)
        {
            try
            {
                var result=await _ludusavi.FindAsync(game.Name,game.PlatformGameId,game.Platform==GamePlatformKind.Steam,game.Platform==GamePlatformKind.Gog,token).ConfigureAwait(false);
                var match=ExtractBestFindMatch(result.Json);
                if(match.Name.Length>0) await _store.SetGameMatchAsync(game.PlayniteId,match.Name,match.Score,token).ConfigureAwait(false);
            }
            catch(Exception ex){_logger.LogWarning(ex,"Could not match {Game}",game.Name);}
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

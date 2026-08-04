using System.Diagnostics;
using GameSaveCenter.Contracts;
using GameSaveCenter.Worker.Configuration;
using GameSaveCenter.Worker.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Services;

/// <summary>Fallback detector for games started from launchers, shortcuts or MOD managers.</summary>
public sealed class ExternalGameProcessDetector : BackgroundService
{
    private readonly WorkerOptions _options;
    private readonly GameCatalogService _catalog;
    private readonly GameSessionCoordinator _sessions;
    private readonly SqliteStateStore _store;
    private readonly ILogger<ExternalGameProcessDetector> _logger;
    private readonly Dictionary<int,DetectedProcess> _detected=new();
    private Dictionary<string,List<GameDescriptorDto>> _processMap = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _processMapRefreshedUtc = DateTime.MinValue;
    private bool _baselineCaptured;

    public ExternalGameProcessDetector(WorkerOptions options,GameCatalogService catalog,GameSessionCoordinator sessions,SqliteStateStore store,ILogger<ExternalGameProcessDetector> logger)
    { _options=options;_catalog=catalog;_sessions=sessions;_store=store;_logger=logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            if(_options.EnableProcessDetection)
            {
                try{await ScanAsync(stoppingToken).ConfigureAwait(false);}catch(Exception ex){_logger.LogWarning(ex,"External process scan failed");}
            }
            await Task.Delay(TimeSpan.FromSeconds(_options.ProcessPollingSeconds),stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ScanAsync(CancellationToken token)
    {
        var games=await _catalog.GetGamesAsync(token).ConfigureAwait(false);
        Dictionary<string,List<GameDescriptorDto>> map;
        if (DateTime.UtcNow - _processMapRefreshedUtc >= TimeSpan.FromSeconds(20))
        {
            map=BuildMap(games);
            foreach(var learned in await _store.GetProcessMappingsAsync(token).ConfigureAwait(false))
                if(learned.Enabled&&games.FirstOrDefault(x=>string.Equals(x.PlayniteId,learned.PlayniteId,StringComparison.OrdinalIgnoreCase)) is { } learnedGame)
                    map[learned.ExecutableName]=new List<GameDescriptorDto>{learnedGame};
            _processMap=map;
            _processMapRefreshedUtc=DateTime.UtcNow;
        }
        else
        {
            map=_processMap;
        }
        var currentGameIds=games.Select(x=>x.PlayniteId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var processes=Process.GetProcesses();var live=new HashSet<int>();
        foreach(var process in processes)
        {
            using(process)
            {
                live.Add(process.Id);if(_detected.ContainsKey(process.Id))continue;
                string name;try{name=process.ProcessName;}catch{continue;}
                if(!map.TryGetValue(name,out var mappedCandidates))continue;
                var candidates=mappedCandidates.Where(x=>currentGameIds.Contains(x.PlayniteId)).ToList();
                if(candidates.Count!=1)continue;
                var game=candidates[0];
                var detected=new DetectedProcess(process.Id,name,game.PlayniteId,game.Name,DateTime.UtcNow);_detected[process.Id]=detected;
                // The first scan is a baseline, not a session start event. This prevents a
                // Worker launched after Playnite from treating every already-running helper
                // process as a newly started game and triggering unexpected backups.
                if (!_baselineCaptured) continue;
                await _sessions.StartAsync(new GameSessionEventDto{PlayniteId=game.PlayniteId,GameName=game.Name,Source=SessionSourceKind.ProcessDetection,ProcessId=process.Id,ProcessName=name,StartedUtc=detected.StartedUtc,LaunchProfile=FindProfile(game,name)},token).ConfigureAwait(false);
            }
        }
        _baselineCaptured=true;
        foreach(var stopped in _detected.Values.Where(x=>!live.Contains(x.ProcessId)).ToList())
        {
            _detected.Remove(stopped.ProcessId);
            // MOD launch chains often contain a loader plus the real game process. Do not
            // close the logical game session until every mapped process for that game exits.
            if(_detected.Values.Any(x=>string.Equals(x.PlayniteId,stopped.PlayniteId,StringComparison.OrdinalIgnoreCase))) continue;
            await _sessions.StopAsync(new GameSessionEventDto{PlayniteId=stopped.PlayniteId,GameName=stopped.GameName,Source=SessionSourceKind.ProcessDetection,ProcessId=stopped.ProcessId,ProcessName=stopped.ProcessName,StoppedUtc=DateTime.UtcNow},token).ConfigureAwait(false);
        }
    }

    private static Dictionary<string,List<GameDescriptorDto>> BuildMap(IEnumerable<GameDescriptorDto> games)
    {
        var map=new Dictionary<string,List<GameDescriptorDto>>(StringComparer.OrdinalIgnoreCase);
        foreach(var game in games.Where(x=>x.IsInstalled))
        {
            var names=new HashSet<string>(game.KnownProcessNames.Where(x=>!string.IsNullOrWhiteSpace(x)).Select(Path.GetFileNameWithoutExtension).Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x!),StringComparer.OrdinalIgnoreCase);
            foreach(var action in game.Actions.Where(x=>x.IsPlayAction||x.IsModLoader))
            {
                var name=Path.GetFileNameWithoutExtension(action.Path);if(!string.IsNullOrWhiteSpace(name))names.Add(name);
            }
            foreach(var name in names)
            {
                if(IsLauncherOnly(name))continue;
                if(!map.TryGetValue(name,out var list)){list=new List<GameDescriptorDto>();map[name]=list;}
                list.Add(game);
            }
        }
        return map;
    }

    private static bool IsLauncherOnly(string name)=>new[]{"steam","steamwebhelper","epicgameslauncher","upc","ubisoftconnect","eadesktop","eabackgroundservice","galaxyclient","gamingservices","explorer"}.Contains(name,StringComparer.OrdinalIgnoreCase);
    private static string FindProfile(GameDescriptorDto game,string process)=>game.Actions.FirstOrDefault(x=>string.Equals(Path.GetFileNameWithoutExtension(x.Path),process,StringComparison.OrdinalIgnoreCase))?.Name??"External";
    private sealed record DetectedProcess(int ProcessId,string ProcessName,string PlayniteId,string GameName,DateTime StartedUtc);
}

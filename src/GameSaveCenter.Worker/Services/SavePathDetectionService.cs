using System.Text.Json;
using GameSaveCenter.Contracts;
using GameSaveCenter.Core.Services;
using GameSaveCenter.Worker.Configuration;
using GameSaveCenter.Worker.Persistence;

namespace GameSaveCenter.Worker.Services;

/// <summary>Creates bounded before/after snapshots and proposes save paths for user confirmation.</summary>
public sealed class SavePathDetectionService
{
    private static readonly HashSet<string> SaveExtensions=new(StringComparer.OrdinalIgnoreCase){".sav",".save",".dat",".bin",".slot",".profile",".json",".xml"};
    private static readonly HashSet<string> CacheExtensions=new(StringComparer.OrdinalIgnoreCase){".log",".tmp",".cache",".dmp",".shader",".bak"};
    private readonly WorkerOptions _options;
    private readonly GameCatalogService _catalog;
    private readonly SqliteStateStore _store;
    private readonly SaveCandidateScorer _scorer=new();

    public SavePathDetectionService(WorkerOptions options,GameCatalogService catalog,SqliteStateStore store)
    { _options=options;_catalog=catalog;_store=store; }

    public async Task<List<SavePathCandidateDto>> DetectAsync(DetectionRequestDto request,CancellationToken token)
    {
        var game=await _catalog.GetGameAsync(request.PlayniteId,token).ConfigureAwait(false)??throw new InvalidOperationException("Game not found.");
        var roots=CandidateRoots(game,request).Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var output=new List<SavePathCandidateDto>();
        foreach(var root in roots)
        {
            token.ThrowIfCancellationRequested();
            foreach(var directory in EnumerateBoundedDirectories(root,3))
            {
                List<FileInfo> files;
                try{files=Directory.EnumerateFiles(directory,"*",SearchOption.TopDirectoryOnly).Select(x=>new FileInfo(x)).Where(x=>x.Exists&&x.LastWriteTimeUtc>DateTime.UtcNow.AddDays(-14)).Take(500).ToList();}
                catch{continue;}
                if(files.Count==0) continue;
                var changed=files.Count(x=>x.LastWriteTimeUtc>DateTime.UtcNow.AddHours(-8));
                if(changed==0) continue;
                var changedFiles=files.Where(x=>x.LastWriteTimeUtc>DateTime.UtcNow.AddHours(-8)).Select(x=>x.FullName).ToList();
                var candidate=_scorer.Score(directory,changedFiles,
                    files.Any(x=>x.LastWriteTimeUtc>DateTime.UtcNow.AddMinutes(-15)),false,
                    directory.Contains("SystemAppData",StringComparison.OrdinalIgnoreCase)&&directory.Contains("wgs",StringComparison.OrdinalIgnoreCase));
                if(candidate.Score<0.35) continue;
                var dto=new SavePathCandidateDto{PlayniteId=game.PlayniteId,Path=directory,Score=candidate.Score,Reasons=candidate.Reasons};output.Add(dto);
                await _store.AddSaveCandidateAsync(game.PlayniteId,directory,candidate.Score,JsonSerializer.Serialize(candidate.Reasons),token).ConfigureAwait(false);
            }
        }
        return output.OrderByDescending(x=>x.Score).Take(50).ToList();
    }

    public async Task<object> AcceptAsync(AcceptSavePathRequestDto request,CancellationToken token)
    {
        var game=await _catalog.GetGameAsync(request.PlayniteId,token).ConfigureAwait(false)??throw new InvalidOperationException("Game not found.");
        var fullPath=Path.GetFullPath(Environment.ExpandEnvironmentVariables(request.Path));
        if(!Directory.Exists(fullPath)) throw new DirectoryNotFoundException(fullPath);
        var drafts=Path.Combine(_options.DataDirectory,"CustomRuleDrafts");
        Directory.CreateDirectory(drafts);
        var safeName=string.Concat(game.Name.Select(ch=>Path.GetInvalidFileNameChars().Contains(ch)?'_':ch));
        var draftPath=Path.Combine(drafts,$"{safeName}-{game.PlayniteId}.json");
        var draft=new
        {
            game=game.Name,playniteId=game.PlayniteId,path=fullPath,includeSubdirectories=request.IncludeSubdirectories,
            note="Review and import into Ludusavi custom games. GameSaveCenter does not silently alter Ludusavi configuration."
        };
        await File.WriteAllTextAsync(draftPath,JsonSerializer.Serialize(draft,new JsonSerializerOptions{WriteIndented=true}),token).ConfigureAwait(false);
        await _store.SetSaveCandidateStatusAsync(game.PlayniteId,fullPath,"Accepted",token).ConfigureAwait(false);
        await _store.AppendAuditAsync("Detection","Accepted save path",JsonSerializer.Serialize(new{game.PlayniteId,fullPath,draftPath}),token).ConfigureAwait(false);
        return new{accepted=true,draftPath};
    }

    private IEnumerable<string> CandidateRoots(GameDescriptorDto game,DetectionRequestDto request)
    {
        var profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(profile,"Saved Games");
        yield return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if(!string.IsNullOrWhiteSpace(game.InstallDirectory))yield return game.InstallDirectory;
        if(request.IncludeXboxWgs) yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Packages");
        foreach(var root in request.AdditionalRoots) yield return Environment.ExpandEnvironmentVariables(root);
    }

    private static IEnumerable<string> EnumerateBoundedDirectories(string root,int depth)
    {
        var queue=new Queue<(string Path,int Depth)>();queue.Enqueue((root,0));var visited=0;
        while(queue.Count>0&&visited<5000)
        {
            var item=queue.Dequeue();visited++;yield return item.Path;if(item.Depth>=depth)continue;
            IEnumerable<string> children;try{children=Directory.EnumerateDirectories(item.Path).Take(300).ToList();}catch{continue;}
            foreach(var child in children)
            {
                var name=Path.GetFileName(child);
                if(name.Equals("Temp",StringComparison.OrdinalIgnoreCase)||name.Equals("Cache",StringComparison.OrdinalIgnoreCase)||name.Equals("Packages",StringComparison.OrdinalIgnoreCase)&&item.Depth>0)continue;
                queue.Enqueue((child,item.Depth+1));
            }
        }
    }
}

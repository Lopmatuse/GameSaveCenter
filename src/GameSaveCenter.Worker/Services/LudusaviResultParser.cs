using System.Text.Json;
using GameSaveCenter.Contracts;
using GameSaveCenter.Core.Models;

namespace GameSaveCenter.Worker.Services;

/// <summary>Defensive parser for Ludusavi's documented general-output JSON schema.</summary>
internal static class LudusaviResultParser
{
    public static BackupSnapshot ParseOperationSnapshot(JsonElement root,string gameName,string backupId,DateTime createdUtc,bool isPreRestore=false)
    {
        var snapshot=new BackupSnapshot{BackupId=backupId,CreatedUtc=createdUtc,SourceDevice=Environment.MachineName,IsPreRestore=isPreRestore};
        if(root.ValueKind!=JsonValueKind.Object || !root.TryGetProperty("games",out var games) || games.ValueKind!=JsonValueKind.Object) return snapshot;
        if(!games.TryGetProperty(gameName,out var game))
        {
            var found=games.EnumerateObject().FirstOrDefault(x=>string.Equals(x.Name,gameName,StringComparison.OrdinalIgnoreCase));
            game=found.Value;
        }
        if(game.ValueKind!=JsonValueKind.Object || !game.TryGetProperty("files",out var files) || files.ValueKind!=JsonValueKind.Object) return snapshot;
        foreach(var file in files.EnumerateObject())
        {
            var bytes=file.Value.TryGetProperty("bytes",out var size)&&size.TryGetInt64(out var parsed)?parsed:0;
            snapshot.Files.Add(new FileManifestEntry{RelativePath=file.Name,SizeBytes=bytes,LastWriteUtc=createdUtc});
            snapshot.TotalBytes+=bytes;
        }
        snapshot.FileCount=snapshot.Files.Count;
        return snapshot;
    }

    public static List<BackupVersionDto> ParseBackupList(JsonElement root,string playniteId,string gameName)
    {
        var output=new List<BackupVersionDto>();
        if(root.ValueKind!=JsonValueKind.Object || !root.TryGetProperty("games",out var games) || games.ValueKind!=JsonValueKind.Object) return output;
        if(!games.TryGetProperty(gameName,out var game) || game.ValueKind!=JsonValueKind.Object || !game.TryGetProperty("backups",out var backups) || backups.ValueKind!=JsonValueKind.Array) return output;
        foreach(var item in backups.EnumerateArray())
        {
            var id=item.TryGetProperty("name",out var name)?name.GetString()??string.Empty:string.Empty;
            var when=item.TryGetProperty("when",out var time)&&DateTime.TryParse(time.GetString(),out var parsed)?parsed.ToUniversalTime():DateTime.MinValue;
            output.Add(new BackupVersionDto
            {
                BackupId=id,PlayniteId=playniteId,LudusaviName=gameName,CreatedUtc=when,IsLocked=item.TryGetProperty("locked",out var locked)&&locked.GetBoolean(),
                Comment=item.TryGetProperty("comment",out var comment)&&comment.ValueKind==JsonValueKind.String?comment.GetString()??string.Empty:string.Empty,
                SourceDevice=Environment.MachineName,OperatingSystem=item.TryGetProperty("os",out var os)&&os.ValueKind==JsonValueKind.String?os.GetString()??string.Empty:string.Empty,
                IsPreRestore=(item.TryGetProperty("comment",out comment)&&comment.ValueKind==JsonValueKind.String&&(comment.GetString()??string.Empty).StartsWith("PreRestore",StringComparison.OrdinalIgnoreCase))
            });
        }
        return output;
    }

    public static bool SomeGamesFailed(JsonElement root)=>root.ValueKind==JsonValueKind.Object&&root.TryGetProperty("errors",out var errors)&&errors.ValueKind==JsonValueKind.Object&&errors.TryGetProperty("someGamesFailed",out var flag)&&flag.ValueKind==JsonValueKind.True;
}

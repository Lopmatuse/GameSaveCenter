using System.Text.Json;
using GameSaveCenter.Contracts;
using GameSaveCenter.Worker.Configuration;
using GameSaveCenter.Worker.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Persistence;

/// <summary>
/// Durable local state. Large binaries remain on disk; SQLite stores identities,
/// summaries and audit history so files stay usable without this application.
/// </summary>
public sealed class SqliteStateStore
{
    private readonly WorkerOptions _options;
    private readonly ILogger<SqliteStateStore> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public SqliteStateStore(WorkerOptions options, ILogger<SqliteStateStore> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken token)
    {
        Directory.CreateDirectory(_options.DataDirectory);
        Directory.CreateDirectory(_options.LogDirectory);
        Directory.CreateDirectory(_options.MediaArchiveDirectory);
        Directory.CreateDirectory(_options.LudusaviBackupDirectory);
        Directory.CreateDirectory(_options.GameToolsDirectory);
        Directory.CreateDirectory(_options.DownloadDirectory);
        await using var connection = Open();
        await connection.OpenAsync(token).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = Schema;
        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "media_sources", "shared_directory", "INTEGER NOT NULL DEFAULT 0", token).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "media", "classification_state", "TEXT NOT NULL DEFAULT 'Assigned'", token).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "media", "classification_reason", "TEXT", token).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "games", "match_input_hash", "TEXT", token).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "games", "last_match_attempt_utc", "TEXT", token).ConfigureAwait(false);
        var normalizeMedia = connection.CreateCommand();
        normalizeMedia.CommandText = @"
UPDATE media
SET classification_state=CASE
    WHEN COALESCE(playnite_id,'')='' THEN 'Inbox'
    ELSE 'Assigned'
END
WHERE COALESCE(classification_state,'')='' OR classification_state='Assigned';";
        await normalizeMedia.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        var mediaClassificationIndex = connection.CreateCommand();
        mediaClassificationIndex.CommandText = "CREATE INDEX IF NOT EXISTS ix_media_classification ON media(classification_state,captured_utc DESC);";
        await mediaClassificationIndex.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        await EnsureBackupVersionSchemaAsync(connection, token).ConfigureAwait(false);
    }

    public async Task<List<GameToolDto>> GetGameToolsAsync(string playniteId, CancellationToken token)
    {
        var tools = new List<GameToolDto>();
        await using var connection = Open();
        await connection.OpenAsync(token).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = @"SELECT tool_id,playnite_id,tool_type,source_type,display_name,enabled,auto_start,
launch_timing,launch_delay_seconds,close_on_game_exit,requires_admin,active_version_id,created_utc,updated_utc
FROM game_tools WHERE playnite_id=$game ORDER BY tool_type,display_name COLLATE NOCASE;";
        command.Parameters.AddWithValue("$game", playniteId);
        await using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                tools.Add(new GameToolDto
                {
                    ToolId=reader.GetString(0),PlayniteId=reader.GetString(1),ToolType=(GameToolType)reader.GetInt32(2),
                    SourceType=(GameToolSourceType)reader.GetInt32(3),DisplayName=reader.GetString(4),
                    Enabled=reader.GetInt32(5)==1,AutoStart=reader.GetInt32(6)==1,
                    LaunchTiming=(GameToolLaunchTiming)reader.GetInt32(7),LaunchDelaySeconds=reader.GetInt32(8),
                    CloseOnGameExit=reader.GetInt32(9)==1,RequiresAdmin=reader.GetInt32(10)==1,
                    ActiveVersionId=reader.IsDBNull(11)?string.Empty:reader.GetString(11),
                    CreatedUtc=DateTime.Parse(reader.GetString(12)).ToUniversalTime(),
                    UpdatedUtc=DateTime.Parse(reader.GetString(13)).ToUniversalTime()
                });
            }
        }
        foreach (var tool in tools)
        {
            var versions = connection.CreateCommand();
            versions.CommandText = @"SELECT version_id,version_name,entry_path,working_directory,arguments,source_url,
file_sha256,download_utc,created_utc FROM game_tool_versions WHERE tool_id=$tool ORDER BY created_utc DESC;";
            versions.Parameters.AddWithValue("$tool", tool.ToolId);
            await using var reader = await versions.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                var path = reader.GetString(2);
                tool.Versions.Add(new GameToolVersionDto
                {
                    VersionId=reader.GetString(0),ToolId=tool.ToolId,VersionName=reader.IsDBNull(1)?"":reader.GetString(1),
                    EntryPath=path,WorkingDirectory=reader.IsDBNull(3)?Path.GetDirectoryName(path)??"":reader.GetString(3),
                    Arguments=reader.IsDBNull(4)?"":reader.GetString(4),SourceUrl=reader.IsDBNull(5)?"":reader.GetString(5),
                    FileSha256=reader.IsDBNull(6)?"":reader.GetString(6),
                    DownloadUtc=reader.IsDBNull(7)?null:DateTime.Parse(reader.GetString(7)).ToUniversalTime(),
                    CreatedUtc=DateTime.Parse(reader.GetString(8)).ToUniversalTime(),IsAvailable=File.Exists(path)
                });
            }
        }
        return tools;
    }

    public async Task<GameToolDto?> GetGameToolAsync(string toolId, CancellationToken token)
    {
        await using var connection = Open();
        await connection.OpenAsync(token).ConfigureAwait(false);
        var find = connection.CreateCommand();
        find.CommandText = "SELECT playnite_id FROM game_tools WHERE tool_id=$id;";
        find.Parameters.AddWithValue("$id", toolId);
        var gameId = await find.ExecuteScalarAsync(token).ConfigureAwait(false) as string;
        if (gameId == null) return null;
        return (await GetGameToolsAsync(gameId, token).ConfigureAwait(false)).FirstOrDefault(x => x.ToolId == toolId);
    }

    public Task UpsertGameToolAsync(GameToolDto tool, GameToolVersionDto version, CancellationToken token) => ExecuteAsync(@"
INSERT INTO game_tools(tool_id,playnite_id,tool_type,source_type,display_name,enabled,auto_start,launch_timing,
launch_delay_seconds,close_on_game_exit,requires_admin,active_version_id,created_utc,updated_utc)
VALUES($id,$game,$type,$source,$name,$enabled,$auto,$timing,$delay,$close,$admin,$version,$created,$updated)
ON CONFLICT(tool_id) DO UPDATE SET display_name=excluded.display_name,active_version_id=excluded.active_version_id,updated_utc=excluded.updated_utc;
INSERT INTO game_tool_versions(version_id,tool_id,version_name,entry_path,working_directory,arguments,source_url,file_sha256,download_utc,created_utc)
VALUES($version,$id,$versionName,$path,$working,$arguments,$url,$hash,$download,$created)
ON CONFLICT(version_id) DO UPDATE SET entry_path=excluded.entry_path,file_sha256=excluded.file_sha256;",
        new Dictionary<string,object?>
        {
            ["$id"]=tool.ToolId,["$game"]=tool.PlayniteId,["$type"]=(int)tool.ToolType,["$source"]=(int)tool.SourceType,
            ["$name"]=tool.DisplayName,["$enabled"]=tool.Enabled?1:0,["$auto"]=tool.AutoStart?1:0,["$timing"]=(int)tool.LaunchTiming,
            ["$delay"]=tool.LaunchDelaySeconds,["$close"]=tool.CloseOnGameExit?1:0,["$admin"]=tool.RequiresAdmin?1:0,
            ["$version"]=version.VersionId,["$versionName"]=version.VersionName,["$path"]=version.EntryPath,
            ["$working"]=version.WorkingDirectory,["$arguments"]=version.Arguments,["$url"]=version.SourceUrl,
            ["$hash"]=version.FileSha256,["$download"]=version.DownloadUtc?.ToString("O"),["$created"]=tool.CreatedUtc.ToString("O"),
            ["$updated"]=tool.UpdatedUtc.ToString("O")
        }, token);

    public Task UpdateGameToolAsync(UpdateGameToolRequestDto update, CancellationToken token) => ExecuteAsync(@"
UPDATE game_tools SET enabled=$enabled,auto_start=$auto,launch_timing=$timing,launch_delay_seconds=$delay,
close_on_game_exit=$close,requires_admin=$admin,
active_version_id=CASE WHEN COALESCE($version,'')='' THEN active_version_id ELSE $version END,updated_utc=$utc
WHERE tool_id=$id;",
        new Dictionary<string,object?>
        {
            ["$id"]=update.ToolId,["$enabled"]=update.Enabled?1:0,["$auto"]=update.AutoStart?1:0,
            ["$timing"]=(int)update.LaunchTiming,["$delay"]=Math.Clamp(update.LaunchDelaySeconds,0,300),
            ["$close"]=update.CloseOnGameExit?1:0,["$admin"]=update.RequiresAdmin?1:0,
            ["$version"]=update.ActiveVersionId,["$utc"]=DateTime.UtcNow.ToString("O")
        }, token);

    public Task DeleteGameToolAsync(string toolId, CancellationToken token) => ExecuteAsync(
        "DELETE FROM game_tools WHERE tool_id=$id;",
        new Dictionary<string,object?> { ["$id"]=toolId }, token);

    public Task ReplaceTrainerCatalogAsync(IEnumerable<TrainerCatalogItemDto> items, CancellationToken token)
        => ExecuteCatalogReplaceAsync(items, token);

    private async Task ExecuteCatalogReplaceAsync(IEnumerable<TrainerCatalogItemDto> items, CancellationToken token)
    {
        await _writeGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await using var connection = Open();
            await connection.OpenAsync(token).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
            foreach (var item in items)
            {
                var command = connection.CreateCommand();
                command.Transaction = (SqliteTransaction)transaction;
                command.CommandText = @"INSERT INTO trainer_catalog(catalog_id,title,normalized_title,page_url,game_version,option_count,last_updated_utc,last_synced_utc)
VALUES($id,$title,$normalized,$url,$version,$options,$updated,$synced)
ON CONFLICT(catalog_id) DO UPDATE SET title=excluded.title,normalized_title=excluded.normalized_title,page_url=excluded.page_url,last_synced_utc=excluded.last_synced_utc;";
                command.Parameters.AddWithValue("$id",item.CatalogId);command.Parameters.AddWithValue("$title",item.Title);
                command.Parameters.AddWithValue("$normalized",item.NormalizedTitle);command.Parameters.AddWithValue("$url",item.PageUrl);
                command.Parameters.AddWithValue("$version",item.GameVersion);command.Parameters.AddWithValue("$options",item.OptionCount);
                command.Parameters.AddWithValue("$updated",(object?)item.LastUpdatedUtc?.ToString("O")??DBNull.Value);
                command.Parameters.AddWithValue("$synced",item.LastSyncedUtc.ToString("O"));
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            await transaction.CommitAsync(token).ConfigureAwait(false);
        }
        finally { _writeGate.Release(); }
    }

    public async Task<List<TrainerCatalogItemDto>> SearchTrainerCatalogAsync(string query, int limit, CancellationToken token)
    {
        var result = new List<TrainerCatalogItemDto>();
        await using var connection=Open(); await connection.OpenAsync(token).ConfigureAwait(false);
        var command=connection.CreateCommand();
        command.CommandText=@"SELECT catalog_id,title,normalized_title,page_url,game_version,option_count,last_updated_utc,last_synced_utc
FROM trainer_catalog WHERE normalized_title LIKE $query OR title LIKE $query
ORDER BY CASE WHEN normalized_title=$exact THEN 0 WHEN normalized_title LIKE $prefix THEN 1 ELSE 2 END,title LIMIT $limit;";
        var normalized = NormalizeSearch(query);
        command.Parameters.AddWithValue("$query","%"+normalized+"%");command.Parameters.AddWithValue("$exact",normalized);
        command.Parameters.AddWithValue("$prefix",normalized+"%");command.Parameters.AddWithValue("$limit",Math.Clamp(limit,1,200));
        await using var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while(await reader.ReadAsync(token).ConfigureAwait(false))result.Add(new TrainerCatalogItemDto
        {
            CatalogId=reader.GetString(0),Title=reader.GetString(1),NormalizedTitle=reader.GetString(2),PageUrl=reader.GetString(3),
            GameVersion=reader.IsDBNull(4)?"":reader.GetString(4),OptionCount=reader.IsDBNull(5)?0:reader.GetInt32(5),
            LastUpdatedUtc=reader.IsDBNull(6)?null:DateTime.Parse(reader.GetString(6)).ToUniversalTime(),
            LastSyncedUtc=DateTime.Parse(reader.GetString(7)).ToUniversalTime()
        });
        return result;
    }

    public Task ReplaceTrainerReleasesAsync(string catalogId, IEnumerable<TrainerReleaseDto> releases, CancellationToken token)
        => ExecuteReleaseReplaceAsync(catalogId,releases,token);

    private async Task ExecuteReleaseReplaceAsync(string catalogId,IEnumerable<TrainerReleaseDto> releases,CancellationToken token)
    {
        await _writeGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await using var connection=Open();await connection.OpenAsync(token).ConfigureAwait(false);
            await using var transaction=await connection.BeginTransactionAsync(token).ConfigureAwait(false);
            var delete=connection.CreateCommand();delete.Transaction=(SqliteTransaction)transaction;
            delete.CommandText="DELETE FROM trainer_releases WHERE catalog_id=$id;";delete.Parameters.AddWithValue("$id",catalogId);
            await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            foreach(var release in releases)
            {
                var command=connection.CreateCommand();command.Transaction=(SqliteTransaction)transaction;
                command.CommandText=@"INSERT INTO trainer_releases(release_id,catalog_id,display_name,download_url,size_bytes,published_utc,last_synced_utc)
VALUES($id,$catalog,$name,$url,$size,$published,$synced);";
                command.Parameters.AddWithValue("$id",release.ReleaseId);command.Parameters.AddWithValue("$catalog",catalogId);
                command.Parameters.AddWithValue("$name",release.DisplayName);command.Parameters.AddWithValue("$url",release.DownloadUrl);
                command.Parameters.AddWithValue("$size",release.SizeBytes);command.Parameters.AddWithValue("$published",(object?)release.PublishedUtc?.ToString("O")??DBNull.Value);
                command.Parameters.AddWithValue("$synced",DateTime.UtcNow.ToString("O"));
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            await transaction.CommitAsync(token).ConfigureAwait(false);
        }
        finally{_writeGate.Release();}
    }

    public async Task<List<TrainerReleaseDto>> GetTrainerReleasesAsync(string catalogId,CancellationToken token)
    {
        var result=new List<TrainerReleaseDto>();await using var connection=Open();await connection.OpenAsync(token).ConfigureAwait(false);
        var command=connection.CreateCommand();command.CommandText=@"SELECT release_id,catalog_id,display_name,download_url,size_bytes,published_utc
FROM trainer_releases WHERE catalog_id=$id ORDER BY COALESCE(published_utc,'') DESC;";command.Parameters.AddWithValue("$id",catalogId);
        await using var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while(await reader.ReadAsync(token).ConfigureAwait(false))result.Add(new TrainerReleaseDto
        {ReleaseId=reader.GetString(0),CatalogId=reader.GetString(1),DisplayName=reader.GetString(2),DownloadUrl=reader.GetString(3),
         SizeBytes=reader.GetInt64(4),PublishedUtc=reader.IsDBNull(5)?null:DateTime.Parse(reader.GetString(5)).ToUniversalTime()});
        return result;
    }

    public async Task<TrainerCatalogItemDto?> GetTrainerCatalogItemAsync(string catalogId,CancellationToken token)
    {
        await using var connection=Open();await connection.OpenAsync(token).ConfigureAwait(false);
        var command=connection.CreateCommand();command.CommandText=@"SELECT catalog_id,title,normalized_title,page_url,game_version,option_count,last_updated_utc,last_synced_utc
FROM trainer_catalog WHERE catalog_id=$id;";command.Parameters.AddWithValue("$id",catalogId);
        await using var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false);if(!await reader.ReadAsync(token).ConfigureAwait(false))return null;
        return new TrainerCatalogItemDto{CatalogId=reader.GetString(0),Title=reader.GetString(1),NormalizedTitle=reader.GetString(2),PageUrl=reader.GetString(3),
            GameVersion=reader.IsDBNull(4)?"":reader.GetString(4),OptionCount=reader.IsDBNull(5)?0:reader.GetInt32(5),
            LastUpdatedUtc=reader.IsDBNull(6)?null:DateTime.Parse(reader.GetString(6)).ToUniversalTime(),LastSyncedUtc=DateTime.Parse(reader.GetString(7)).ToUniversalTime()};
    }

    public async Task<TrainerReleaseDto?> GetTrainerReleaseAsync(string releaseId,CancellationToken token)
    {
        await using var connection=Open();await connection.OpenAsync(token).ConfigureAwait(false);
        var command=connection.CreateCommand();command.CommandText=@"SELECT release_id,catalog_id,display_name,download_url,size_bytes,published_utc
FROM trainer_releases WHERE release_id=$id;";command.Parameters.AddWithValue("$id",releaseId);
        await using var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false);if(!await reader.ReadAsync(token).ConfigureAwait(false))return null;
        return new TrainerReleaseDto{ReleaseId=reader.GetString(0),CatalogId=reader.GetString(1),DisplayName=reader.GetString(2),
            DownloadUrl=reader.GetString(3),SizeBytes=reader.GetInt64(4),PublishedUtc=reader.IsDBNull(5)?null:DateTime.Parse(reader.GetString(5)).ToUniversalTime()};
    }

    private static string NormalizeSearch(string value)
        => new string((value??string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static async Task EnsureColumnAsync(SqliteConnection connection, string table, string column, string definition, CancellationToken token)
    {
        var inspect = connection.CreateCommand();
        inspect.CommandText = $"PRAGMA table_info({table});";
        var found = false;
        await using (var reader = await inspect.ExecuteReaderAsync(token).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
        }
        if (found) return;
        var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        await alter.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    private static async Task EnsureBackupVersionSchemaAsync(SqliteConnection connection, CancellationToken token)
    {
        var inspect = connection.CreateCommand();
        inspect.CommandText = "PRAGMA table_info(backup_versions);";
        var backupIdPrimary = false;
        var playniteIdPrimary = false;
        await using (var reader = await inspect.ExecuteReaderAsync(token).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                var name = reader.GetString(1);
                var primaryOrder = reader.GetInt32(5);
                if (string.Equals(name, "backup_id", StringComparison.OrdinalIgnoreCase)) backupIdPrimary = primaryOrder > 0;
                if (string.Equals(name, "playnite_id", StringComparison.OrdinalIgnoreCase)) playniteIdPrimary = primaryOrder > 0;
            }
        }
        if (backupIdPrimary && playniteIdPrimary) return;

        await using var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
        var migrate = connection.CreateCommand();
        migrate.Transaction = (SqliteTransaction)transaction;
        migrate.CommandText = @"
DROP TABLE IF EXISTS backup_versions_v2;
CREATE TABLE backup_versions_v2(
    backup_id TEXT NOT NULL,playnite_id TEXT NOT NULL,ludusavi_name TEXT NOT NULL,created_utc TEXT NOT NULL,
    total_bytes INTEGER NOT NULL,file_count INTEGER NOT NULL,is_locked INTEGER NOT NULL DEFAULT 0,comment TEXT,
    source_device TEXT,operating_system TEXT,is_pre_restore INTEGER NOT NULL DEFAULT 0,manifest_json TEXT,
    PRIMARY KEY(playnite_id,backup_id));
INSERT OR REPLACE INTO backup_versions_v2(backup_id,playnite_id,ludusavi_name,created_utc,total_bytes,file_count,is_locked,comment,source_device,operating_system,is_pre_restore,manifest_json)
SELECT backup_id,playnite_id,ludusavi_name,created_utc,total_bytes,file_count,is_locked,comment,source_device,operating_system,is_pre_restore,manifest_json FROM backup_versions;
DROP TABLE backup_versions;
ALTER TABLE backup_versions_v2 RENAME TO backup_versions;
CREATE INDEX IF NOT EXISTS ix_backup_versions_game_time ON backup_versions(playnite_id,created_utc DESC);";
        await migrate.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        await transaction.CommitAsync(token).ConfigureAwait(false);
    }

    public async Task UpsertGamesAsync(IEnumerable<GameDescriptorDto> games, CancellationToken token)
    {
        await _writeGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await using var connection = Open();
            await connection.OpenAsync(token).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
            foreach (var game in games)
            {
                var command = connection.CreateCommand();
                command.Transaction = (SqliteTransaction)transaction;
                command.CommandText = @"
INSERT INTO games(playnite_id, name, platform, platform_game_id, install_directory, descriptor_json, match_input_hash, last_match_attempt_utc, updated_utc)
VALUES($id,$name,$platform,$platformId,$install,$json,$matchHash,$utc,$utc)
ON CONFLICT(playnite_id) DO UPDATE SET
 name=excluded.name, platform=excluded.platform, platform_game_id=excluded.platform_game_id,
 install_directory=excluded.install_directory, descriptor_json=excluded.descriptor_json,
 match_input_hash=CASE WHEN COALESCE(games.match_input_hash,'')='' THEN excluded.match_input_hash ELSE games.match_input_hash END,
 last_match_attempt_utc=CASE WHEN games.last_match_attempt_utc IS NULL THEN excluded.last_match_attempt_utc ELSE games.last_match_attempt_utc END,
 updated_utc=excluded.updated_utc;";
                command.Parameters.AddWithValue("$id", game.PlayniteId);
                command.Parameters.AddWithValue("$name", game.Name);
                command.Parameters.AddWithValue("$platform", (int)game.Platform);
                command.Parameters.AddWithValue("$platformId", game.PlatformGameId ?? string.Empty);
                command.Parameters.AddWithValue("$install", game.InstallDirectory ?? string.Empty);
                command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(game, _json));
                command.Parameters.AddWithValue("$matchHash", GameMatchInput.CreateHash(game));
                command.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            await transaction.CommitAsync(token).ConfigureAwait(false);
        }
        finally { _writeGate.Release(); }
    }

    public async Task<List<GameDescriptorDto>> GetGamesAsync(CancellationToken token)
    {
        var result = new List<GameDescriptorDto>();
        await using var connection = Open();
        await connection.OpenAsync(token).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT descriptor_json FROM games ORDER BY name COLLATE NOCASE;";
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            var item = JsonSerializer.Deserialize<GameDescriptorDto>(reader.GetString(0), _json);
            if (item != null) result.Add(item);
        }
        return result;
    }

    public async Task<GameDescriptorDto?> GetGameAsync(string playniteId, CancellationToken token)
    {
        await using var connection = Open();
        await connection.OpenAsync(token).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT descriptor_json FROM games WHERE playnite_id=$id;";
        command.Parameters.AddWithValue("$id", playniteId);
        var value = await command.ExecuteScalarAsync(token).ConfigureAwait(false) as string;
        return value == null ? null : JsonSerializer.Deserialize<GameDescriptorDto>(value, _json);
    }

    public async Task<Dictionary<string, GameMatchCacheEntry>> GetGameMatchCacheAsync(CancellationToken token)
    {
        var result = new Dictionary<string, GameMatchCacheEntry>(StringComparer.OrdinalIgnoreCase);
        await using var connection = Open();
        await connection.OpenAsync(token).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = @"SELECT playnite_id,descriptor_json,ludusavi_name,match_confidence,match_input_hash,last_match_attempt_utc
FROM games;";
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            var descriptor = JsonSerializer.Deserialize<GameDescriptorDto>(reader.GetString(1), _json);
            if (descriptor == null) continue;
            result[reader.GetString(0)] = new GameMatchCacheEntry
            {
                Descriptor = descriptor,
                LudusaviName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Confidence = reader.IsDBNull(3) ? 0 : reader.GetDouble(3),
                MatchInputHash = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                LastMatchAttemptUtc = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5)).ToUniversalTime()
            };
        }
        return result;
    }

    public async Task SetGameMatchAsync(string playniteId, string ludusaviName, double confidence, string matchInputHash, CancellationToken token)
    {
        await ExecuteAsync(@"UPDATE games
SET ludusavi_name=$name,match_confidence=$confidence,match_input_hash=$hash,last_match_attempt_utc=$utc,updated_utc=$utc
WHERE playnite_id=$id;",
            new Dictionary<string, object?> { ["$id"] = playniteId, ["$name"] = ludusaviName, ["$confidence"] = confidence, ["$hash"] = matchInputHash, ["$utc"] = DateTime.UtcNow.ToString("O") }, token).ConfigureAwait(false);
    }

    public async Task<Dictionary<string, (string Name, double Confidence)>> GetGameMatchesAsync(CancellationToken token)
    {
        var result = new Dictionary<string, (string, double)>(StringComparer.OrdinalIgnoreCase);
        await using var connection = Open();
        await connection.OpenAsync(token).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT playnite_id, ludusavi_name, match_confidence FROM games;";
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
            result[reader.GetString(0)] = (reader.IsDBNull(1) ? string.Empty : reader.GetString(1), reader.IsDBNull(2) ? 0 : reader.GetDouble(2));
        return result;
    }

    public async Task<BackupPolicyDto> GetPolicyAsync(string playniteId, CancellationToken token)
    {
        await using var connection = Open();
        await connection.OpenAsync(token).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT policy_json FROM game_policies WHERE playnite_id=$id;";
        command.Parameters.AddWithValue("$id", playniteId);
        var value = await command.ExecuteScalarAsync(token).ConfigureAwait(false) as string;
        return string.IsNullOrWhiteSpace(value) ? new BackupPolicyDto() : JsonSerializer.Deserialize<BackupPolicyDto>(value, _json) ?? new BackupPolicyDto();
    }

    public async Task<List<DashboardGameRecord>> GetDashboardGameRecordsAsync(CancellationToken token)
    {
        var result = new List<DashboardGameRecord>();
        await using var connection = Open();
        await connection.OpenAsync(token).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT g.descriptor_json,g.ludusavi_name,g.match_confidence,
       COALESCE(b.version_count,0),b.last_backup_utc,
       COALESCE(m.media_count,0),m.last_media_utc,p.policy_json
FROM games g
LEFT JOIN (
    SELECT playnite_id,COUNT(*) AS version_count,MAX(created_utc) AS last_backup_utc
    FROM backup_versions GROUP BY playnite_id
) b ON b.playnite_id=g.playnite_id
LEFT JOIN (
    SELECT playnite_id,COUNT(*) AS media_count,MAX(captured_utc) AS last_media_utc
    FROM media WHERE classification_state='Assigned' GROUP BY playnite_id
) m ON m.playnite_id=g.playnite_id
LEFT JOIN game_policies p ON p.playnite_id=g.playnite_id
ORDER BY g.name COLLATE NOCASE;";
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            var descriptor = JsonSerializer.Deserialize<GameDescriptorDto>(reader.GetString(0), _json);
            if (descriptor == null) continue;
            result.Add(new DashboardGameRecord
            {
                Descriptor = descriptor,
                LudusaviName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                MatchConfidence = reader.IsDBNull(2) ? 0 : reader.GetDouble(2),
                BackupVersionCount = reader.GetInt32(3),
                LastBackupUtc = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)).ToUniversalTime(),
                MediaCount = reader.GetInt32(5),
                LastMediaUtc = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)).ToUniversalTime(),
                Policy = reader.IsDBNull(7)
                    ? new BackupPolicyDto()
                    : JsonSerializer.Deserialize<BackupPolicyDto>(reader.GetString(7), _json) ?? new BackupPolicyDto()
            });
        }
        return result;
    }

    public Task SetPolicyAsync(string playniteId, BackupPolicyDto policy, CancellationToken token) => ExecuteAsync(@"
INSERT INTO game_policies(playnite_id,policy_json,updated_utc) VALUES($id,$json,$utc)
ON CONFLICT(playnite_id) DO UPDATE SET policy_json=excluded.policy_json,updated_utc=excluded.updated_utc;",
        new Dictionary<string, object?> { ["$id"] = playniteId, ["$json"] = JsonSerializer.Serialize(policy, _json), ["$utc"] = DateTime.UtcNow.ToString("O") }, token);

    public Task AddSessionAsync(GameSessionEventDto session, CancellationToken token) => ExecuteAsync(@"
INSERT INTO sessions(session_id,playnite_id,source,process_id,process_name,launch_profile,started_utc,stopped_utc,elapsed_seconds)
VALUES($session,$game,$source,$pid,$process,$profile,$started,$stopped,$elapsed)
ON CONFLICT(session_id) DO UPDATE SET stopped_utc=excluded.stopped_utc,elapsed_seconds=excluded.elapsed_seconds;",
        new Dictionary<string, object?>
        {
            ["$session"] = session.SessionId, ["$game"] = session.PlayniteId, ["$source"] = (int)session.Source,
            ["$pid"] = session.ProcessId, ["$process"] = session.ProcessName, ["$profile"] = session.LaunchProfile,
            ["$started"] = session.StartedUtc.ToString("O"), ["$stopped"] = session.StoppedUtc?.ToString("O"), ["$elapsed"] = session.ElapsedSeconds
        }, token);

    public async Task<List<GameSessionEventDto>> GetOpenSessionsAsync(CancellationToken token)
    {
        var result = new List<GameSessionEventDto>();
        await using var connection = Open();
        await connection.OpenAsync(token).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT session_id,playnite_id,source,process_id,process_name,launch_profile,started_utc,elapsed_seconds FROM sessions WHERE stopped_utc IS NULL;";
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            result.Add(new GameSessionEventDto
            {
                SessionId = reader.GetString(0), PlayniteId = reader.GetString(1), Source = (SessionSourceKind)reader.GetInt32(2),
                ProcessId = reader.IsDBNull(3) ? null : reader.GetInt32(3), ProcessName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                LaunchProfile = reader.IsDBNull(5) ? string.Empty : reader.GetString(5), StartedUtc = DateTime.Parse(reader.GetString(6)).ToUniversalTime(),
                ElapsedSeconds = reader.GetInt64(7)
            });
        }
        return result;
    }

    public async Task<GameSessionEventDto?> GetSessionAsync(string sessionId, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return null;
        await using var connection = Open();
        await connection.OpenAsync(token).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = @"SELECT session_id,playnite_id,source,process_id,process_name,launch_profile,started_utc,stopped_utc,elapsed_seconds
FROM sessions WHERE session_id=$session LIMIT 1;";
        command.Parameters.AddWithValue("$session", sessionId);
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false)) return null;
        return new GameSessionEventDto
        {
            SessionId = reader.GetString(0),
            PlayniteId = reader.GetString(1),
            Source = (SessionSourceKind)reader.GetInt32(2),
            ProcessId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            ProcessName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            LaunchProfile = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            StartedUtc = DateTime.Parse(reader.GetString(6)).ToUniversalTime(),
            StoppedUtc = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7)).ToUniversalTime(),
            ElapsedSeconds = reader.IsDBNull(8) ? 0 : reader.GetInt64(8)
        };
    }

    public async Task<bool> HasOverlappingGameSessionAsync(string playniteId, DateTime startUtc, DateTime stopUtc, CancellationToken token)
    {
        await using var connection = Open();
        await connection.OpenAsync(token).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = @"SELECT COUNT(*) FROM sessions
WHERE playnite_id<>$game
  AND started_utc<=$stop
  AND (stopped_utc IS NULL OR stopped_utc>=$start);";
        command.Parameters.AddWithValue("$game", playniteId);
        command.Parameters.AddWithValue("$start", startUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$stop", stopUtc.ToUniversalTime().ToString("O"));
        return Convert.ToInt32(await command.ExecuteScalarAsync(token).ConfigureAwait(false)) > 0;
    }

    public Task MarkInterruptedTasksAsync(CancellationToken token) => ExecuteAsync(@"
UPDATE tasks
SET state=$failed, progress=CASE WHEN progress>99 THEN 99 ELSE progress END, message=$message,
    finished_utc=$finished, error_code=$errorCode, error_message=$errorMessage
WHERE state IN ($queued,$running);",
        new Dictionary<string, object?>
        {
            ["$failed"]=(int)TaskState.Failed,
            ["$queued"]=(int)TaskState.Queued,
            ["$running"]=(int)TaskState.Running,
            ["$message"]="Worker 重启前任务未完成",
            ["$finished"]=DateTime.UtcNow.ToString("O"),
            ["$errorCode"]="WORKER_RESTARTED",
            ["$errorMessage"]="Worker 在任务完成前退出或重启；请确认目标文件状态后重新执行。"
        },token);

    public Task AddOrUpdateTaskAsync(TaskStatusDto task, CancellationToken token) => ExecuteAsync(@"
INSERT INTO tasks(task_id,task_type,game_id,game_name,state,progress,message,created_utc,started_utc,finished_utc,error_code,error_message)
VALUES($id,$type,$game,$name,$state,$progress,$message,$created,$started,$finished,$errorCode,$errorMessage)
ON CONFLICT(task_id) DO UPDATE SET state=excluded.state,progress=excluded.progress,message=excluded.message,
 started_utc=excluded.started_utc,finished_utc=excluded.finished_utc,error_code=excluded.error_code,error_message=excluded.error_message;",
        new Dictionary<string, object?>
        {
            ["$id"] = task.TaskId, ["$type"] = task.TaskType, ["$game"] = task.GameId, ["$name"] = task.GameName,
            ["$state"] = (int)task.State, ["$progress"] = task.ProgressPercent, ["$message"] = task.Message,
            ["$created"] = task.CreatedUtc.ToString("O"), ["$started"] = task.StartedUtc?.ToString("O"), ["$finished"] = task.FinishedUtc?.ToString("O"),
            ["$errorCode"] = task.ErrorCode, ["$errorMessage"] = task.ErrorMessage
        }, token);

    public async Task<List<TaskStatusDto>> GetRecentTasksAsync(int limit, CancellationToken token)
    {
        var result = new List<TaskStatusDto>();
        await using var connection = Open();
        await connection.OpenAsync(token).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT task_id,task_type,game_id,game_name,state,progress,message,created_utc,started_utc,finished_utc,error_code,error_message FROM tasks ORDER BY created_utc DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 500));
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            result.Add(new TaskStatusDto
            {
                TaskId=reader.GetString(0), TaskType=reader.GetString(1), GameId=reader.IsDBNull(2)?string.Empty:reader.GetString(2),
                GameName=reader.IsDBNull(3)?string.Empty:reader.GetString(3), State=(TaskState)reader.GetInt32(4), ProgressPercent=reader.GetInt32(5),
                Message=reader.IsDBNull(6)?string.Empty:reader.GetString(6), CreatedUtc=DateTime.Parse(reader.GetString(7)).ToUniversalTime(),
                StartedUtc=reader.IsDBNull(8)?null:DateTime.Parse(reader.GetString(8)).ToUniversalTime(),
                FinishedUtc=reader.IsDBNull(9)?null:DateTime.Parse(reader.GetString(9)).ToUniversalTime(),
                ErrorCode=reader.IsDBNull(10)?string.Empty:reader.GetString(10), ErrorMessage=reader.IsDBNull(11)?string.Empty:reader.GetString(11)
            });
        }
        return result;
    }

    public Task AddFindingAsync(string playniteId, ValidationFindingDto finding, CancellationToken token) => ExecuteAsync(@"
INSERT INTO findings(finding_id,playnite_id,severity,code,title,detail,suggested_action,created_utc,resolved)
VALUES($id,$game,$severity,$code,$title,$detail,$action,$utc,0);",
        new Dictionary<string, object?> { ["$id"] = Guid.NewGuid().ToString("N"), ["$game"] = playniteId, ["$severity"] = (int)finding.Severity,
            ["$code"] = finding.Code, ["$title"] = finding.Title, ["$detail"] = finding.Detail, ["$action"] = finding.SuggestedAction, ["$utc"] = DateTime.UtcNow.ToString("O") }, token);

    public async Task<List<ValidationFindingDto>> GetOpenFindingsAsync(int limit, CancellationToken token)
    {
        var result = new List<ValidationFindingDto>();
        await using var connection = Open();
        await connection.OpenAsync(token).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT playnite_id,severity,code,title,detail,suggested_action FROM findings WHERE resolved=0 ORDER BY created_utc DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 500));
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false)) result.Add(new ValidationFindingDto
        {
            PlayniteId=reader.IsDBNull(0)?string.Empty:reader.GetString(0), Severity=(FindingSeverity)reader.GetInt32(1), Code=reader.GetString(2), Title=reader.GetString(3), Detail=reader.IsDBNull(4)?string.Empty:reader.GetString(4), SuggestedAction=reader.IsDBNull(5)?string.Empty:reader.GetString(5)
        });
        return result;
    }

    public Task AddBackupVersionAsync(BackupVersionDto version, string manifestJson, CancellationToken token) => ExecuteAsync(@"
INSERT INTO backup_versions(backup_id,playnite_id,ludusavi_name,created_utc,total_bytes,file_count,is_locked,comment,source_device,operating_system,is_pre_restore,manifest_json)
VALUES($id,$game,$ludusavi,$created,$bytes,$count,$locked,$comment,$device,$os,$pre,$manifest)
ON CONFLICT(playnite_id,backup_id) DO UPDATE SET ludusavi_name=excluded.ludusavi_name,created_utc=excluded.created_utc,total_bytes=CASE WHEN excluded.total_bytes=0 AND backup_versions.total_bytes>0 THEN backup_versions.total_bytes ELSE excluded.total_bytes END,file_count=CASE WHEN excluded.file_count=0 AND backup_versions.file_count>0 THEN backup_versions.file_count ELSE excluded.file_count END,is_locked=excluded.is_locked,comment=excluded.comment,source_device=excluded.source_device,operating_system=excluded.operating_system,is_pre_restore=excluded.is_pre_restore,manifest_json=CASE WHEN excluded.manifest_json='{}' THEN backup_versions.manifest_json ELSE excluded.manifest_json END;",
        new Dictionary<string, object?> { ["$id"]=version.BackupId,["$game"]=version.PlayniteId,["$ludusavi"]=version.LudusaviName,["$created"]=version.CreatedUtc.ToString("O"),
            ["$bytes"]=version.TotalBytes,["$count"]=version.FileCount,["$locked"]=version.IsLocked?1:0,["$comment"]=version.Comment,["$device"]=version.SourceDevice,
            ["$os"]=version.OperatingSystem,["$pre"]=version.IsPreRestore?1:0,["$manifest"]=manifestJson }, token);

    public async Task<List<BackupVersionDto>> GetBackupVersionsAsync(string playniteId, CancellationToken token)
    {
        var result = new List<BackupVersionDto>();
        await using var connection=Open(); await connection.OpenAsync(token).ConfigureAwait(false);
        var command=connection.CreateCommand();
        command.CommandText="SELECT backup_id,ludusavi_name,created_utc,total_bytes,file_count,is_locked,comment,source_device,operating_system,is_pre_restore FROM backup_versions WHERE playnite_id=$id ORDER BY created_utc DESC;";
        command.Parameters.AddWithValue("$id",playniteId);
        await using var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while(await reader.ReadAsync(token).ConfigureAwait(false)) result.Add(new BackupVersionDto
        {
            BackupId=reader.GetString(0),PlayniteId=playniteId,LudusaviName=reader.GetString(1),CreatedUtc=DateTime.Parse(reader.GetString(2)).ToUniversalTime(),
            TotalBytes=reader.GetInt64(3),FileCount=reader.GetInt32(4),IsLocked=reader.GetInt32(5)==1,Comment=reader.IsDBNull(6)?string.Empty:reader.GetString(6),SourceDevice=reader.IsDBNull(7)?string.Empty:reader.GetString(7),
            OperatingSystem=reader.IsDBNull(8)?string.Empty:reader.GetString(8),IsPreRestore=reader.GetInt32(9)==1
        });
        return result;
    }

    /// <summary>Returns one newest indexed backup per game for a content-free device sidecar.</summary>
    public async Task<List<DeviceBackupSummaryDto>> GetLatestBackupSummariesAsync(CancellationToken token)
    {
        var result = new List<DeviceBackupSummaryDto>();
        await using var connection = Open(); await connection.OpenAsync(token).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT b.playnite_id,g.name,b.backup_id,b.created_utc,b.total_bytes,b.file_count
FROM backup_versions b
JOIN games g ON g.playnite_id=b.playnite_id
JOIN (SELECT playnite_id,MAX(created_utc) AS newest FROM backup_versions GROUP BY playnite_id) latest
  ON latest.playnite_id=b.playnite_id AND latest.newest=b.created_utc
ORDER BY g.name COLLATE NOCASE;";
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false)) result.Add(new DeviceBackupSummaryDto
        {
            PlayniteId=reader.GetString(0),GameName=reader.GetString(1),BackupId=reader.GetString(2),
            CreatedUtc=DateTime.Parse(reader.GetString(3)).ToUniversalTime(),TotalBytes=reader.GetInt64(4),FileCount=reader.GetInt32(5)
        });
        return result;
    }

    public async Task<List<ProcessMappingDto>> GetProcessMappingsAsync(CancellationToken token)
    {
        var result=new List<ProcessMappingDto>();await using var connection=Open();await connection.OpenAsync(token).ConfigureAwait(false);
        var command=connection.CreateCommand();command.CommandText="SELECT executable_name,playnite_id,game_name,enabled,created_utc FROM process_mappings ORDER BY game_name COLLATE NOCASE,executable_name COLLATE NOCASE;";
        await using var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while(await reader.ReadAsync(token).ConfigureAwait(false))result.Add(new ProcessMappingDto{ExecutableName=reader.GetString(0),PlayniteId=reader.GetString(1),GameName=reader.GetString(2),Enabled=reader.GetInt32(3)==1,CreatedUtc=DateTime.Parse(reader.GetString(4)).ToUniversalTime()});return result;
    }
    public Task UpsertProcessMappingAsync(ProcessMappingDto value,CancellationToken token)=>ExecuteAsync("INSERT INTO process_mappings(executable_name,playnite_id,game_name,enabled,created_utc) VALUES($exe,$game,$name,$enabled,$utc) ON CONFLICT(executable_name) DO UPDATE SET playnite_id=excluded.playnite_id,game_name=excluded.game_name,enabled=excluded.enabled;",new Dictionary<string,object?>{{"$exe",value.ExecutableName},{"$game",value.PlayniteId},{"$name",value.GameName},{"$enabled",value.Enabled?1:0},{"$utc",value.CreatedUtc.ToString("O")}},token);
    public Task DeleteProcessMappingAsync(string executableName,CancellationToken token)=>ExecuteAsync("DELETE FROM process_mappings WHERE executable_name=$exe;",new Dictionary<string,object?>{{"$exe",executableName}},token);

    public async Task RemoveMissingBackupVersionsAsync(string playniteId, IReadOnlyCollection<string> activeBackupIds, CancellationToken token)
    {
        await _writeGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await using var connection = Open();
            await connection.OpenAsync(token).ConfigureAwait(false);
            var command = connection.CreateCommand();
            if (activeBackupIds.Count == 0)
            {
                command.CommandText = "DELETE FROM backup_versions WHERE playnite_id=$game;";
                command.Parameters.AddWithValue("$game", playniteId);
            }
            else
            {
                var parameterNames = activeBackupIds.Select((_, index) => $"$id{index}").ToArray();
                command.CommandText = $"DELETE FROM backup_versions WHERE playnite_id=$game AND backup_id NOT IN ({string.Join(",", parameterNames)});";
                command.Parameters.AddWithValue("$game", playniteId);
                var index = 0;
                foreach (var id in activeBackupIds) command.Parameters.AddWithValue($"$id{index++}", id);
            }
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }
        finally { _writeGate.Release(); }
    }

    public async Task<string> GetBackupManifestAsync(string playniteId,string backupId,CancellationToken token)
    {
        await using var connection=Open(); await connection.OpenAsync(token).ConfigureAwait(false);
        var command=connection.CreateCommand();
        command.CommandText="SELECT manifest_json FROM backup_versions WHERE playnite_id=$game AND backup_id=$backup;";
        command.Parameters.AddWithValue("$game",playniteId);command.Parameters.AddWithValue("$backup",backupId);
        return await command.ExecuteScalarAsync(token).ConfigureAwait(false) as string ?? "[]";
    }

    public async Task<bool> MediaHashExistsAsync(string sha256, CancellationToken token)
    {
        await using var connection=Open(); await connection.OpenAsync(token).ConfigureAwait(false);
        var command=connection.CreateCommand(); command.CommandText="SELECT 1 FROM media WHERE sha256=$hash LIMIT 1;"; command.Parameters.AddWithValue("$hash",sha256);
        return await command.ExecuteScalarAsync(token).ConfigureAwait(false) != null;
    }

    public Task AddMediaAsync(MediaItemDto item, CancellationToken token) => ExecuteAsync(@"
INSERT OR IGNORE INTO media(media_id,playnite_id,kind,source,archive_path,original_path,captured_utc,size_bytes,sha256,is_favorite,comment,cloud_state,classification_state,classification_reason)
VALUES($id,$game,$kind,$source,$archive,$original,$captured,$size,$hash,$favorite,$comment,$cloud,$classification,$reason);",
        new Dictionary<string, object?> { ["$id"]=item.MediaId,["$game"]=item.PlayniteId,["$kind"]=(int)item.Kind,["$source"]=(int)item.Source,["$archive"]=item.ArchivePath,
            ["$original"]=item.OriginalPath,["$captured"]=item.CapturedUtc.ToString("O"),["$size"]=item.SizeBytes,["$hash"]=item.Sha256,["$favorite"]=item.IsFavorite?1:0,["$comment"]=item.Comment,["$cloud"]=item.CloudState,
            ["$classification"]=string.IsNullOrWhiteSpace(item.ClassificationState)?"Assigned":item.ClassificationState,["$reason"]=item.ClassificationReason }, token);

    public async Task<List<MediaItemDto>> GetMediaAsync(string playniteId, int limit, CancellationToken token)
    {
        var result=new List<MediaItemDto>(); await using var connection=Open(); await connection.OpenAsync(token).ConfigureAwait(false);
        var command=connection.CreateCommand(); command.CommandText="SELECT media_id,kind,source,archive_path,original_path,captured_utc,size_bytes,sha256,is_favorite,comment,cloud_state,classification_state,classification_reason FROM media WHERE playnite_id=$id AND classification_state='Assigned' ORDER BY captured_utc DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$id",playniteId);command.Parameters.AddWithValue("$limit",Math.Clamp(limit,1,5000));
        await using var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while(await reader.ReadAsync(token).ConfigureAwait(false)) result.Add(new MediaItemDto
        { MediaId=reader.GetString(0),PlayniteId=playniteId,Kind=(MediaKind)reader.GetInt32(1),Source=(MediaSourceKind)reader.GetInt32(2),ArchivePath=reader.GetString(3),OriginalPath=reader.GetString(4),
          CapturedUtc=DateTime.Parse(reader.GetString(5)).ToUniversalTime(),SizeBytes=reader.GetInt64(6),Sha256=reader.GetString(7),IsFavorite=reader.GetInt32(8)==1,Comment=reader.IsDBNull(9)?string.Empty:reader.GetString(9),CloudState=reader.IsDBNull(10)?string.Empty:reader.GetString(10),
          ClassificationState=reader.IsDBNull(11)?"Assigned":reader.GetString(11),ClassificationReason=reader.IsDBNull(12)?string.Empty:reader.GetString(12)});
        return result;
    }

    public async Task<List<MediaItemDto>> GetUnassignedMediaAsync(int limit, CancellationToken token)
    {
        var result=new List<MediaItemDto>(); await using var connection=Open(); await connection.OpenAsync(token).ConfigureAwait(false);
        var command=connection.CreateCommand(); command.CommandText=@"SELECT media_id,playnite_id,kind,source,archive_path,original_path,captured_utc,size_bytes,sha256,is_favorite,comment,cloud_state,classification_state,classification_reason
FROM media
WHERE classification_state='Inbox'
ORDER BY captured_utc DESC
LIMIT $limit;";
        command.Parameters.AddWithValue("$limit",Math.Clamp(limit,1,5000));
        await using var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while(await reader.ReadAsync(token).ConfigureAwait(false)) result.Add(ReadMedia(reader));
        return result;
    }

    public async Task<MediaItemDto?> GetMediaByIdAsync(string mediaId,CancellationToken token)
    {
        await using var connection=Open(); await connection.OpenAsync(token).ConfigureAwait(false);
        var command=connection.CreateCommand();command.CommandText=@"SELECT media_id,playnite_id,kind,source,archive_path,original_path,captured_utc,size_bytes,sha256,is_favorite,comment,cloud_state,classification_state,classification_reason
FROM media WHERE media_id=$id LIMIT 1;";
        command.Parameters.AddWithValue("$id",mediaId);
        await using var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        return await reader.ReadAsync(token).ConfigureAwait(false)?ReadMedia(reader):null;
    }

    private static MediaItemDto ReadMedia(SqliteDataReader reader)=>new()
    {
        MediaId=reader.GetString(0),PlayniteId=reader.IsDBNull(1)?string.Empty:reader.GetString(1),Kind=(MediaKind)reader.GetInt32(2),Source=(MediaSourceKind)reader.GetInt32(3),
        ArchivePath=reader.GetString(4),OriginalPath=reader.GetString(5),CapturedUtc=DateTime.Parse(reader.GetString(6)).ToUniversalTime(),SizeBytes=reader.GetInt64(7),Sha256=reader.GetString(8),
        IsFavorite=reader.GetInt32(9)==1,Comment=reader.IsDBNull(10)?string.Empty:reader.GetString(10),CloudState=reader.IsDBNull(11)?string.Empty:reader.GetString(11),
        ClassificationState=reader.IsDBNull(12)?"Assigned":reader.GetString(12),ClassificationReason=reader.IsDBNull(13)?string.Empty:reader.GetString(13)
    };

    public Task AddMediaSourceAsync(MediaSourceRuleDto source,CancellationToken token) => ExecuteAsync(@"
INSERT INTO media_sources(source_id,playnite_id,source_kind,root_path,include_pattern,enabled,shared_directory) VALUES($id,$game,$kind,$root,$pattern,$enabled,$shared)
ON CONFLICT(source_id) DO UPDATE SET playnite_id=excluded.playnite_id,source_kind=excluded.source_kind,root_path=excluded.root_path,include_pattern=excluded.include_pattern,enabled=excluded.enabled,shared_directory=excluded.shared_directory;",
        new Dictionary<string,object?>{["$id"]=string.IsNullOrWhiteSpace(source.SourceId)?Guid.NewGuid().ToString("N"):source.SourceId,["$game"]=source.PlayniteId,["$kind"]=(int)source.SourceKind,["$root"]=source.RootPath,["$pattern"]=source.IncludePattern,["$enabled"]=source.Enabled?1:0,["$shared"]=source.SharedDirectory?1:0},token);

    public async Task<List<MediaSourceRuleDto>> GetMediaSourcesAsync(string playniteId,CancellationToken token)
    {
        var result=new List<MediaSourceRuleDto>();
        await using var connection=Open();await connection.OpenAsync(token).ConfigureAwait(false);
        var command=connection.CreateCommand();command.CommandText="SELECT source_id,playnite_id,source_kind,root_path,include_pattern,enabled,shared_directory FROM media_sources WHERE playnite_id=$game OR COALESCE(playnite_id,'')='';";
        command.Parameters.AddWithValue("$game",playniteId);
        await using var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while(await reader.ReadAsync(token).ConfigureAwait(false))result.Add(new MediaSourceRuleDto
        {SourceId=reader.GetString(0),PlayniteId=reader.IsDBNull(1)?string.Empty:reader.GetString(1),SourceKind=(MediaSourceKind)reader.GetInt32(2),RootPath=reader.GetString(3),IncludePattern=reader.IsDBNull(4)?"*":reader.GetString(4),Enabled=reader.GetInt32(5)==1,SharedDirectory=!reader.IsDBNull(6)&&reader.GetInt32(6)==1});
        return result;
    }

    public async Task<List<MediaSourceRuleDto>> GetSharedMediaSourcesAsync(CancellationToken token)
    {
        var result=new List<MediaSourceRuleDto>();
        await using var connection=Open();await connection.OpenAsync(token).ConfigureAwait(false);
        var command=connection.CreateCommand();command.CommandText="SELECT source_id,playnite_id,source_kind,root_path,include_pattern,enabled,shared_directory FROM media_sources WHERE enabled=1 AND shared_directory=1;";
        await using var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while(await reader.ReadAsync(token).ConfigureAwait(false))result.Add(new MediaSourceRuleDto
        {SourceId=reader.GetString(0),PlayniteId=reader.IsDBNull(1)?string.Empty:reader.GetString(1),SourceKind=(MediaSourceKind)reader.GetInt32(2),RootPath=reader.GetString(3),IncludePattern=reader.IsDBNull(4)?"*":reader.GetString(4),Enabled=reader.GetInt32(5)==1,SharedDirectory=!reader.IsDBNull(6)&&reader.GetInt32(6)==1});
        return result;
    }

    public Task AddSaveCandidateAsync(string playniteId, string path, double score, string reasonsJson, CancellationToken token) => ExecuteAsync(@"
UPDATE save_candidates
SET score=$score,reasons_json=$reasons,status='Pending',created_utc=$utc
WHERE candidate_id=(
    SELECT candidate_id FROM save_candidates
    WHERE playnite_id=$game AND path=$path AND status<>'Accepted'
    ORDER BY created_utc DESC LIMIT 1
);
INSERT INTO save_candidates(candidate_id,playnite_id,path,score,reasons_json,status,created_utc)
SELECT $id,$game,$path,$score,$reasons,'Pending',$utc
WHERE changes()=0
  AND NOT EXISTS(
      SELECT 1 FROM save_candidates
      WHERE playnite_id=$game AND path=$path AND status='Accepted'
  );",
        new Dictionary<string, object?> { ["$id"]=Guid.NewGuid().ToString("N"),["$game"]=playniteId,["$path"]=path,["$score"]=score,["$reasons"]=reasonsJson,["$utc"]=DateTime.UtcNow.ToString("O")},token);

    public Task AssignMediaAsync(string mediaId,string targetPlayniteId,string archivePath,CancellationToken token)=>ExecuteAsync(@"
UPDATE media
SET playnite_id=$game,archive_path=$archive,classification_state='Assigned',classification_reason='',cloud_state='Pending'
WHERE media_id=$id;",
        new Dictionary<string,object?>{["$id"]=mediaId,["$game"]=targetPlayniteId,["$archive"]=archivePath},token);

    public Task IgnoreMediaAsync(string mediaId,string archivePath,CancellationToken token)=>ExecuteAsync(@"
UPDATE media
SET playnite_id='',archive_path=$archive,classification_state='Ignored',classification_reason='用户已忽略',cloud_state='NotApplicable'
WHERE media_id=$id AND classification_state='Inbox';",
        new Dictionary<string,object?>{["$id"]=mediaId,["$archive"]=archivePath},token);

    public Task UpdateMediaCloudStateAsync(string playniteId, string state, CancellationToken token) => ExecuteAsync(
        "UPDATE media SET cloud_state=$state WHERE playnite_id=$game;",
        new Dictionary<string, object?> { ["$game"]=playniteId, ["$state"]=state }, token);

    public async Task<List<SavePathCandidateDto>> GetSaveCandidatesAsync(string playniteId, CancellationToken token)
    {
        var result=new List<SavePathCandidateDto>();
        await using var connection=Open(); await connection.OpenAsync(token).ConfigureAwait(false);
        var command=connection.CreateCommand();
        command.CommandText=@"SELECT s.path,s.score,s.reasons_json,s.status
FROM save_candidates s
WHERE s.playnite_id=$game
  AND s.candidate_id=(
      SELECT x.candidate_id FROM save_candidates x
      WHERE x.playnite_id=s.playnite_id AND x.path=s.path
      ORDER BY CASE x.status WHEN 'Accepted' THEN 0 WHEN 'Pending' THEN 1 ELSE 2 END,
               x.created_utc DESC
      LIMIT 1
  )
ORDER BY CASE s.status WHEN 'Pending' THEN 0 WHEN 'Accepted' THEN 1 ELSE 2 END,
         s.score DESC
LIMIT 100;";
        command.Parameters.AddWithValue("$game",playniteId);
        await using var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while(await reader.ReadAsync(token).ConfigureAwait(false)) result.Add(new SavePathCandidateDto
        {
            PlayniteId=playniteId, Path=reader.GetString(0), Score=reader.GetDouble(1),
            Reasons=JsonSerializer.Deserialize<List<string>>(reader.IsDBNull(2)?"[]":reader.GetString(2),_json)??new List<string>(),
            Status=reader.IsDBNull(3)?"Pending":reader.GetString(3)
        });
        return result;
    }

    public Task SetSaveCandidateStatusAsync(string playniteId,string path,string status,CancellationToken token) => ExecuteAsync(
        "UPDATE save_candidates SET status=$status WHERE playnite_id=$game AND path=$path;",
        new Dictionary<string, object?> { ["$game"]=playniteId,["$path"]=path,["$status"]=status },token);

    public async Task<(int Games,int Matched,int Media,int Unassigned)> GetCountsAsync(CancellationToken token)
    {
        await using var connection=Open(); await connection.OpenAsync(token).ConfigureAwait(false);
        async Task<int> Scalar(string sql){var c=connection.CreateCommand();c.CommandText=sql;return Convert.ToInt32(await c.ExecuteScalarAsync(token).ConfigureAwait(false));}
        return (await Scalar("SELECT COUNT(*) FROM games;"),await Scalar("SELECT COUNT(*) FROM games WHERE COALESCE(ludusavi_name,'')<>'';"),await Scalar("SELECT COUNT(*) FROM media WHERE classification_state='Assigned';"),await Scalar("SELECT COUNT(*) FROM media WHERE classification_state='Inbox';"));
    }

    public Task AppendAuditAsync(string category, string message, string detailJson, CancellationToken token) => ExecuteAsync(
        "INSERT INTO audit_log(audit_id,category,message,detail_json,created_utc) VALUES($id,$category,$message,$detail,$utc);",
        new Dictionary<string, object?> { ["$id"]=Guid.NewGuid().ToString("N"),["$category"]=category,["$message"]=message,["$detail"]=detailJson,["$utc"]=DateTime.UtcNow.ToString("O")},token);


    public async Task<List<AuditLogEntryDto>> GetAuditAsync(int limit, CancellationToken token)
    {
        var result=new List<AuditLogEntryDto>();
        await using var connection=Open();await connection.OpenAsync(token).ConfigureAwait(false);
        var command=connection.CreateCommand();
        command.CommandText="SELECT category,message,detail_json,created_utc FROM audit_log ORDER BY created_utc DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit",Math.Clamp(limit,1,1000));
        await using var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while(await reader.ReadAsync(token).ConfigureAwait(false)) result.Add(new AuditLogEntryDto
        {
            Category=reader.GetString(0),Message=reader.GetString(1),DetailJson=reader.IsDBNull(2)?"{}":reader.GetString(2),CreatedUtc=DateTime.Parse(reader.GetString(3)).ToUniversalTime()
        });
        return result;
    }

    private async Task ExecuteAsync(string sql, IReadOnlyDictionary<string, object?> parameters, CancellationToken token)
    {
        await _writeGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await using var connection=Open(); await connection.OpenAsync(token).ConfigureAwait(false);
            var command=connection.CreateCommand();command.CommandText=sql;
            foreach(var item in parameters) command.Parameters.AddWithValue(item.Key,item.Value??DBNull.Value);
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }
        catch(Exception ex){_logger.LogError(ex,"SQLite operation failed");throw;}
        finally{_writeGate.Release();}
    }

    private SqliteConnection Open() => new($"Data Source={_options.DatabasePath};Mode=ReadWriteCreate;Cache=Shared;Foreign Keys=True");

    private const string Schema = @"
PRAGMA journal_mode=WAL;
CREATE TABLE IF NOT EXISTS games(playnite_id TEXT PRIMARY KEY,name TEXT NOT NULL,platform INTEGER NOT NULL,platform_game_id TEXT,install_directory TEXT,descriptor_json TEXT NOT NULL,ludusavi_name TEXT,match_confidence REAL DEFAULT 0,match_input_hash TEXT,last_match_attempt_utc TEXT,last_backup_utc TEXT,last_media_sync_utc TEXT,health_state TEXT DEFAULT 'Unknown',cloud_state TEXT DEFAULT 'Disabled',updated_utc TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS game_policies(playnite_id TEXT PRIMARY KEY,policy_json TEXT NOT NULL,updated_utc TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS sessions(session_id TEXT PRIMARY KEY,playnite_id TEXT NOT NULL,source INTEGER NOT NULL,process_id INTEGER,process_name TEXT,launch_profile TEXT,started_utc TEXT NOT NULL,stopped_utc TEXT,elapsed_seconds INTEGER DEFAULT 0);
CREATE TABLE IF NOT EXISTS tasks(task_id TEXT PRIMARY KEY,task_type TEXT NOT NULL,game_id TEXT,game_name TEXT,state INTEGER NOT NULL,progress INTEGER NOT NULL,message TEXT,created_utc TEXT NOT NULL,started_utc TEXT,finished_utc TEXT,error_code TEXT,error_message TEXT);
CREATE TABLE IF NOT EXISTS findings(finding_id TEXT PRIMARY KEY,playnite_id TEXT,severity INTEGER NOT NULL,code TEXT NOT NULL,title TEXT NOT NULL,detail TEXT,suggested_action TEXT,created_utc TEXT NOT NULL,resolved INTEGER NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS backup_versions(backup_id TEXT NOT NULL,playnite_id TEXT NOT NULL,ludusavi_name TEXT NOT NULL,created_utc TEXT NOT NULL,total_bytes INTEGER NOT NULL,file_count INTEGER NOT NULL,is_locked INTEGER NOT NULL DEFAULT 0,comment TEXT,source_device TEXT,operating_system TEXT,is_pre_restore INTEGER NOT NULL DEFAULT 0,manifest_json TEXT,PRIMARY KEY(playnite_id,backup_id));
CREATE TABLE IF NOT EXISTS media(media_id TEXT PRIMARY KEY,playnite_id TEXT,kind INTEGER NOT NULL,source INTEGER NOT NULL,archive_path TEXT NOT NULL,original_path TEXT NOT NULL,captured_utc TEXT NOT NULL,size_bytes INTEGER NOT NULL,sha256 TEXT NOT NULL UNIQUE,is_favorite INTEGER NOT NULL DEFAULT 0,comment TEXT,cloud_state TEXT NOT NULL DEFAULT 'Pending',classification_state TEXT NOT NULL DEFAULT 'Assigned',classification_reason TEXT);
CREATE TABLE IF NOT EXISTS media_sources(source_id TEXT PRIMARY KEY,playnite_id TEXT,source_kind INTEGER NOT NULL,root_path TEXT NOT NULL,include_pattern TEXT,enabled INTEGER NOT NULL DEFAULT 1,shared_directory INTEGER NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS save_candidates(candidate_id TEXT PRIMARY KEY,playnite_id TEXT NOT NULL,path TEXT NOT NULL,score REAL NOT NULL,reasons_json TEXT,status TEXT NOT NULL,created_utc TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS audit_log(audit_id TEXT PRIMARY KEY,category TEXT NOT NULL,message TEXT NOT NULL,detail_json TEXT,created_utc TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS game_tools(tool_id TEXT PRIMARY KEY,playnite_id TEXT NOT NULL,tool_type INTEGER NOT NULL,source_type INTEGER NOT NULL,display_name TEXT NOT NULL,enabled INTEGER NOT NULL DEFAULT 1,auto_start INTEGER NOT NULL DEFAULT 0,launch_timing INTEGER NOT NULL DEFAULT 1,launch_delay_seconds INTEGER NOT NULL DEFAULT 8,close_on_game_exit INTEGER NOT NULL DEFAULT 0,requires_admin INTEGER NOT NULL DEFAULT 0,active_version_id TEXT,created_utc TEXT NOT NULL,updated_utc TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS game_tool_versions(version_id TEXT PRIMARY KEY,tool_id TEXT NOT NULL REFERENCES game_tools(tool_id) ON DELETE CASCADE,version_name TEXT,entry_path TEXT NOT NULL,working_directory TEXT,arguments TEXT,source_url TEXT,file_sha256 TEXT,download_utc TEXT,created_utc TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS trainer_catalog(catalog_id TEXT PRIMARY KEY,title TEXT NOT NULL,normalized_title TEXT NOT NULL,page_url TEXT NOT NULL,game_version TEXT,option_count INTEGER NOT NULL DEFAULT 0,last_updated_utc TEXT,last_synced_utc TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS trainer_releases(release_id TEXT PRIMARY KEY,catalog_id TEXT NOT NULL REFERENCES trainer_catalog(catalog_id) ON DELETE CASCADE,display_name TEXT NOT NULL,download_url TEXT NOT NULL,size_bytes INTEGER NOT NULL DEFAULT 0,published_utc TEXT,last_synced_utc TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS process_mappings(executable_name TEXT PRIMARY KEY,playnite_id TEXT NOT NULL,game_name TEXT NOT NULL,enabled INTEGER NOT NULL DEFAULT 1,created_utc TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS ix_tasks_created ON tasks(created_utc DESC);
CREATE INDEX IF NOT EXISTS ix_backup_versions_game_time ON backup_versions(playnite_id,created_utc DESC);
CREATE INDEX IF NOT EXISTS ix_media_game ON media(playnite_id,captured_utc DESC);
CREATE INDEX IF NOT EXISTS ix_sessions_open ON sessions(stopped_utc);
CREATE INDEX IF NOT EXISTS ix_save_candidates_game_path ON save_candidates(playnite_id,path,created_utc DESC);
CREATE INDEX IF NOT EXISTS ix_game_tools_game ON game_tools(playnite_id,tool_type);
CREATE INDEX IF NOT EXISTS ix_game_tool_versions_tool ON game_tool_versions(tool_id,created_utc DESC);
CREATE INDEX IF NOT EXISTS ix_trainer_catalog_title ON trainer_catalog(normalized_title);
";
}

public sealed class GameMatchCacheEntry
{
    public GameDescriptorDto Descriptor { get; set; } = new();
    public string LudusaviName { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string MatchInputHash { get; set; } = string.Empty;
    public DateTime? LastMatchAttemptUtc { get; set; }
}

public sealed class DashboardGameRecord
{
    public GameDescriptorDto Descriptor { get; set; } = new();
    public string LudusaviName { get; set; } = string.Empty;
    public double MatchConfidence { get; set; }
    public int BackupVersionCount { get; set; }
    public DateTime? LastBackupUtc { get; set; }
    public int MediaCount { get; set; }
    public DateTime? LastMediaUtc { get; set; }
    public BackupPolicyDto Policy { get; set; } = new();
}

using System.Text.Json;
using GameSaveCenter.Contracts;
using GameSaveCenter.Worker.Configuration;
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
        await using var connection = Open();
        await connection.OpenAsync(token).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = Schema;
        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "media_sources", "shared_directory", "INTEGER NOT NULL DEFAULT 0", token).ConfigureAwait(false);
        await EnsureBackupVersionSchemaAsync(connection, token).ConfigureAwait(false);
    }

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
INSERT INTO games(playnite_id, name, platform, platform_game_id, install_directory, descriptor_json, updated_utc)
VALUES($id,$name,$platform,$platformId,$install,$json,$utc)
ON CONFLICT(playnite_id) DO UPDATE SET
 name=excluded.name, platform=excluded.platform, platform_game_id=excluded.platform_game_id,
 install_directory=excluded.install_directory, descriptor_json=excluded.descriptor_json, updated_utc=excluded.updated_utc;";
                command.Parameters.AddWithValue("$id", game.PlayniteId);
                command.Parameters.AddWithValue("$name", game.Name);
                command.Parameters.AddWithValue("$platform", (int)game.Platform);
                command.Parameters.AddWithValue("$platformId", game.PlatformGameId ?? string.Empty);
                command.Parameters.AddWithValue("$install", game.InstallDirectory ?? string.Empty);
                command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(game, _json));
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

    public async Task SetGameMatchAsync(string playniteId, string ludusaviName, double confidence, CancellationToken token)
    {
        await ExecuteAsync(@"UPDATE games SET ludusavi_name=$name, match_confidence=$confidence, updated_utc=$utc WHERE playnite_id=$id;",
            new Dictionary<string, object?> { ["$id"] = playniteId, ["$name"] = ludusaviName, ["$confidence"] = confidence, ["$utc"] = DateTime.UtcNow.ToString("O") }, token).ConfigureAwait(false);
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
INSERT OR IGNORE INTO media(media_id,playnite_id,kind,source,archive_path,original_path,captured_utc,size_bytes,sha256,is_favorite,comment,cloud_state)
VALUES($id,$game,$kind,$source,$archive,$original,$captured,$size,$hash,$favorite,$comment,$cloud);",
        new Dictionary<string, object?> { ["$id"]=item.MediaId,["$game"]=item.PlayniteId,["$kind"]=(int)item.Kind,["$source"]=(int)item.Source,["$archive"]=item.ArchivePath,
            ["$original"]=item.OriginalPath,["$captured"]=item.CapturedUtc.ToString("O"),["$size"]=item.SizeBytes,["$hash"]=item.Sha256,["$favorite"]=item.IsFavorite?1:0,["$comment"]=item.Comment,["$cloud"]=item.CloudState }, token);

    public async Task<List<MediaItemDto>> GetMediaAsync(string playniteId, int limit, CancellationToken token)
    {
        var result=new List<MediaItemDto>(); await using var connection=Open(); await connection.OpenAsync(token).ConfigureAwait(false);
        var command=connection.CreateCommand(); command.CommandText="SELECT media_id,kind,source,archive_path,original_path,captured_utc,size_bytes,sha256,is_favorite,comment,cloud_state FROM media WHERE playnite_id=$id ORDER BY captured_utc DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$id",playniteId);command.Parameters.AddWithValue("$limit",Math.Clamp(limit,1,5000));
        await using var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while(await reader.ReadAsync(token).ConfigureAwait(false)) result.Add(new MediaItemDto
        { MediaId=reader.GetString(0),PlayniteId=playniteId,Kind=(MediaKind)reader.GetInt32(1),Source=(MediaSourceKind)reader.GetInt32(2),ArchivePath=reader.GetString(3),OriginalPath=reader.GetString(4),
          CapturedUtc=DateTime.Parse(reader.GetString(5)).ToUniversalTime(),SizeBytes=reader.GetInt64(6),Sha256=reader.GetString(7),IsFavorite=reader.GetInt32(8)==1,Comment=reader.IsDBNull(9)?string.Empty:reader.GetString(9),CloudState=reader.IsDBNull(10)?string.Empty:reader.GetString(10)});
        return result;
    }

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

    public Task AddSaveCandidateAsync(string playniteId, string path, double score, string reasonsJson, CancellationToken token) => ExecuteAsync(@"
INSERT INTO save_candidates(candidate_id,playnite_id,path,score,reasons_json,status,created_utc)
VALUES($id,$game,$path,$score,$reasons,'Pending',$utc);",
        new Dictionary<string, object?> { ["$id"]=Guid.NewGuid().ToString("N"),["$game"]=playniteId,["$path"]=path,["$score"]=score,["$reasons"]=reasonsJson,["$utc"]=DateTime.UtcNow.ToString("O")},token);

    public Task ReassignMediaAsync(string mediaId, string targetPlayniteId, CancellationToken token) => ExecuteAsync(
        "UPDATE media SET playnite_id=$game WHERE media_id=$id;",
        new Dictionary<string, object?> { ["$id"]=mediaId, ["$game"]=targetPlayniteId }, token);

    public Task UpdateMediaCloudStateAsync(string playniteId, string state, CancellationToken token) => ExecuteAsync(
        "UPDATE media SET cloud_state=$state WHERE playnite_id=$game;",
        new Dictionary<string, object?> { ["$game"]=playniteId, ["$state"]=state }, token);

    public async Task<List<SavePathCandidateDto>> GetSaveCandidatesAsync(string playniteId, CancellationToken token)
    {
        var result=new List<SavePathCandidateDto>();
        await using var connection=Open(); await connection.OpenAsync(token).ConfigureAwait(false);
        var command=connection.CreateCommand();
        command.CommandText="SELECT path,score,reasons_json,status FROM save_candidates WHERE playnite_id=$game ORDER BY score DESC,created_utc DESC LIMIT 100;";
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
        return (await Scalar("SELECT COUNT(*) FROM games;"),await Scalar("SELECT COUNT(*) FROM games WHERE COALESCE(ludusavi_name,'')<>'';"),await Scalar("SELECT COUNT(*) FROM media;"),await Scalar("SELECT COUNT(*) FROM media WHERE COALESCE(playnite_id,'')='';"));
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
CREATE TABLE IF NOT EXISTS games(playnite_id TEXT PRIMARY KEY,name TEXT NOT NULL,platform INTEGER NOT NULL,platform_game_id TEXT,install_directory TEXT,descriptor_json TEXT NOT NULL,ludusavi_name TEXT,match_confidence REAL DEFAULT 0,last_backup_utc TEXT,last_media_sync_utc TEXT,health_state TEXT DEFAULT 'Unknown',cloud_state TEXT DEFAULT 'Disabled',updated_utc TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS game_policies(playnite_id TEXT PRIMARY KEY,policy_json TEXT NOT NULL,updated_utc TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS sessions(session_id TEXT PRIMARY KEY,playnite_id TEXT NOT NULL,source INTEGER NOT NULL,process_id INTEGER,process_name TEXT,launch_profile TEXT,started_utc TEXT NOT NULL,stopped_utc TEXT,elapsed_seconds INTEGER DEFAULT 0);
CREATE TABLE IF NOT EXISTS tasks(task_id TEXT PRIMARY KEY,task_type TEXT NOT NULL,game_id TEXT,game_name TEXT,state INTEGER NOT NULL,progress INTEGER NOT NULL,message TEXT,created_utc TEXT NOT NULL,started_utc TEXT,finished_utc TEXT,error_code TEXT,error_message TEXT);
CREATE TABLE IF NOT EXISTS findings(finding_id TEXT PRIMARY KEY,playnite_id TEXT,severity INTEGER NOT NULL,code TEXT NOT NULL,title TEXT NOT NULL,detail TEXT,suggested_action TEXT,created_utc TEXT NOT NULL,resolved INTEGER NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS backup_versions(backup_id TEXT NOT NULL,playnite_id TEXT NOT NULL,ludusavi_name TEXT NOT NULL,created_utc TEXT NOT NULL,total_bytes INTEGER NOT NULL,file_count INTEGER NOT NULL,is_locked INTEGER NOT NULL DEFAULT 0,comment TEXT,source_device TEXT,operating_system TEXT,is_pre_restore INTEGER NOT NULL DEFAULT 0,manifest_json TEXT,PRIMARY KEY(playnite_id,backup_id));
CREATE TABLE IF NOT EXISTS media(media_id TEXT PRIMARY KEY,playnite_id TEXT,kind INTEGER NOT NULL,source INTEGER NOT NULL,archive_path TEXT NOT NULL,original_path TEXT NOT NULL,captured_utc TEXT NOT NULL,size_bytes INTEGER NOT NULL,sha256 TEXT NOT NULL UNIQUE,is_favorite INTEGER NOT NULL DEFAULT 0,comment TEXT,cloud_state TEXT NOT NULL DEFAULT 'Pending');
CREATE TABLE IF NOT EXISTS media_sources(source_id TEXT PRIMARY KEY,playnite_id TEXT,source_kind INTEGER NOT NULL,root_path TEXT NOT NULL,include_pattern TEXT,enabled INTEGER NOT NULL DEFAULT 1,shared_directory INTEGER NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS save_candidates(candidate_id TEXT PRIMARY KEY,playnite_id TEXT NOT NULL,path TEXT NOT NULL,score REAL NOT NULL,reasons_json TEXT,status TEXT NOT NULL,created_utc TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS audit_log(audit_id TEXT PRIMARY KEY,category TEXT NOT NULL,message TEXT NOT NULL,detail_json TEXT,created_utc TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS ix_tasks_created ON tasks(created_utc DESC);
CREATE INDEX IF NOT EXISTS ix_backup_versions_game_time ON backup_versions(playnite_id,created_utc DESC);
CREATE INDEX IF NOT EXISTS ix_media_game ON media(playnite_id,captured_utc DESC);
CREATE INDEX IF NOT EXISTS ix_sessions_open ON sessions(stopped_utc);
";
}

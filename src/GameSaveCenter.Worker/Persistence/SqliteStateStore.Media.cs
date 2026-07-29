using GameSaveCenter.Contracts;
using Microsoft.Data.Sqlite;

namespace GameSaveCenter.Worker.Persistence;

public sealed partial class SqliteStateStore
{
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

    public async Task<MediaStorageSummaryDto> GetMediaSummaryAsync(string playniteId, CancellationToken token)
    {
        await using var connection=Open(); await connection.OpenAsync(token).ConfigureAwait(false);
        var command=connection.CreateCommand();
        command.CommandText=@"SELECT
COUNT(*),
COALESCE(SUM(CASE WHEN kind=$screenshot THEN 1 ELSE 0 END),0),
COALESCE(SUM(CASE WHEN kind=$video THEN 1 ELSE 0 END),0),
COALESCE(SUM(CASE WHEN is_favorite=1 THEN 1 ELSE 0 END),0),
COALESCE(SUM(size_bytes),0)
FROM media
WHERE playnite_id=$id AND classification_state='Assigned';";
        command.Parameters.AddWithValue("$id",playniteId);
        command.Parameters.AddWithValue("$screenshot",(int)MediaKind.Screenshot);
        command.Parameters.AddWithValue("$video",(int)MediaKind.VideoClip);
        await using var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        if(!await reader.ReadAsync(token).ConfigureAwait(false))return new MediaStorageSummaryDto();
        return new MediaStorageSummaryDto
        {
            TotalCount=checked((int)reader.GetInt64(0)),
            ScreenshotCount=checked((int)reader.GetInt64(1)),
            VideoCount=checked((int)reader.GetInt64(2)),
            FavoriteCount=checked((int)reader.GetInt64(3)),
            TotalBytes=reader.GetInt64(4)
        };
    }

    public Task UpdateMediaMetadataAsync(MediaMetadataUpdateDto update,CancellationToken token)=>ExecuteAsync(@"
UPDATE media
SET is_favorite=$favorite,comment=$comment
WHERE media_id=$id;",
        new Dictionary<string,object?>
        {
            ["$id"]=update.MediaId,
            ["$favorite"]=update.IsFavorite?1:0,
            ["$comment"]=(update.Comment??string.Empty).Trim()
        },token);

    public async Task UpdateMediaMetadataBatchAsync(MediaMetadataBatchUpdateDto update,CancellationToken token)
    {
        await _writeGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await using var connection=Open();
            await connection.OpenAsync(token).ConfigureAwait(false);
            await using var transaction=await connection.BeginTransactionAsync(token).ConfigureAwait(false);
            var updated=0;
            foreach(var mediaId in update.MediaIds)
            {
                var command=connection.CreateCommand();
                command.Transaction=(SqliteTransaction)transaction;
                command.CommandText=@"UPDATE media
SET is_favorite=CASE WHEN $update_favorite=1 THEN $favorite ELSE is_favorite END,
    comment=CASE WHEN $update_comment=1 THEN $comment ELSE comment END
WHERE media_id=$id;";
                command.Parameters.AddWithValue("$id",mediaId);
                command.Parameters.AddWithValue("$update_favorite",update.IsFavorite.HasValue?1:0);
                command.Parameters.AddWithValue("$favorite",update.IsFavorite==true?1:0);
                command.Parameters.AddWithValue("$update_comment",update.UpdateComment?1:0);
                command.Parameters.AddWithValue("$comment",update.Comment);
                updated+=await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            if(updated!=update.MediaIds.Count)
                throw new InvalidOperationException("一个或多个媒体记录不存在，批量更新已取消。");
            await transaction.CommitAsync(token).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
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
}

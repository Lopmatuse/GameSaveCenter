using Microsoft.Data.Sqlite;

namespace GameSaveCenter.Worker.Persistence;

public sealed partial class SqliteStateStore
{
    public Task UpsertCloudRetryAsync(CloudRetryQueueEntry entry, CancellationToken token) => ExecuteAsync(@"
INSERT INTO cloud_retry_queue(playnite_id,attempt_count,next_attempt_utc,last_error,created_utc,updated_utc)
VALUES($game,$attempts,$next,$error,$created,$updated)
ON CONFLICT(playnite_id) DO UPDATE SET
attempt_count=excluded.attempt_count,next_attempt_utc=excluded.next_attempt_utc,last_error=excluded.last_error,updated_utc=excluded.updated_utc;",
        new Dictionary<string, object?>
        {
            ["$game"] = entry.PlayniteId, ["$attempts"] = entry.RetryCount,
            ["$next"] = entry.NextAttemptUtc.ToUniversalTime().ToString("O"), ["$error"] = entry.LastError,
            ["$created"] = entry.CreatedUtc.ToUniversalTime().ToString("O"), ["$updated"] = entry.UpdatedUtc.ToUniversalTime().ToString("O")
        }, token);

    public Task RemoveCloudRetryAsync(string playniteId, CancellationToken token) => ExecuteAsync(
        "DELETE FROM cloud_retry_queue WHERE playnite_id=$game;",
        new Dictionary<string, object?> { ["$game"] = playniteId }, token);

    public Task DeferCloudRetryAsync(string playniteId, DateTime nextAttemptUtc, string error, CancellationToken token) => ExecuteAsync(@"
UPDATE cloud_retry_queue
SET next_attempt_utc=$next,last_error=$error,updated_utc=$updated
WHERE playnite_id=$game;",
        new Dictionary<string, object?>
        {
            ["$game"] = playniteId, ["$next"] = nextAttemptUtc.ToUniversalTime().ToString("O"),
            ["$error"] = error, ["$updated"] = DateTime.UtcNow.ToString("O")
        }, token);

    public async Task<CloudRetryQueueEntry?> GetCloudRetryAsync(string playniteId, CancellationToken token)
    {
        await using var connection = Open();
        await connection.OpenAsync(token).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = @"SELECT playnite_id,attempt_count,next_attempt_utc,last_error,created_utc,updated_utc
FROM cloud_retry_queue WHERE playnite_id=$game;";
        command.Parameters.AddWithValue("$game", playniteId);
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        return await reader.ReadAsync(token).ConfigureAwait(false) ? ReadCloudRetry(reader) : null;
    }

    public async Task<List<CloudRetryQueueEntry>> GetDueCloudRetriesAsync(DateTime nowUtc, int limit, CancellationToken token)
    {
        var result = new List<CloudRetryQueueEntry>();
        await using var connection = Open();
        await connection.OpenAsync(token).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = @"SELECT playnite_id,attempt_count,next_attempt_utc,last_error,created_utc,updated_utc
FROM cloud_retry_queue WHERE next_attempt_utc <= $now ORDER BY next_attempt_utc LIMIT $limit;";
        command.Parameters.AddWithValue("$now", nowUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 100));
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false)) result.Add(ReadCloudRetry(reader));
        return result;
    }

    private static CloudRetryQueueEntry ReadCloudRetry(SqliteDataReader reader) => new()
    {
        PlayniteId = reader.GetString(0), RetryCount = reader.GetInt32(1),
        NextAttemptUtc = DateTime.Parse(reader.GetString(2)).ToUniversalTime(),
        LastError = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
        CreatedUtc = DateTime.Parse(reader.GetString(4)).ToUniversalTime(),
        UpdatedUtc = DateTime.Parse(reader.GetString(5)).ToUniversalTime()
    };
}

public sealed class CloudRetryQueueEntry
{
    public string PlayniteId { get; set; } = string.Empty;
    /// <summary>How many automatic retry attempts have been scheduled after the original failure.</summary>
    public int RetryCount { get; set; }
    public DateTime NextAttemptUtc { get; set; }
    public string LastError { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

using GameSaveCenter.Worker.Configuration;
using GameSaveCenter.Worker.Persistence;
using GameSaveCenter.Worker.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameSaveCenter.Worker.Tests;

public sealed class CloudRetryPersistenceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "GameSaveCenter.Tests", Guid.NewGuid().ToString("N"));
    private readonly WorkerOptions options;
    private readonly SqliteStateStore store;

    public CloudRetryPersistenceTests()
    {
        options = new WorkerOptions
        {
            DataDirectory = root,
            LudusaviBackupDirectory = Path.Combine(root, "Saves"),
            MediaArchiveDirectory = Path.Combine(root, "Media")
        };
        store = NewStore();
        store.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void RetryPolicy_UsesBoundedDeterministicBackoff()
    {
        var now = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
        var expectedMinutes = new[] { 1, 5, 15, 60, 240, 720 };

        Assert.Equal(expectedMinutes.Length, CloudRetryPolicy.MaximumAutomaticRetries);
        for (var retry = 1; retry <= expectedMinutes.Length; retry++)
            Assert.Equal(now.AddMinutes(expectedMinutes[retry - 1]), CloudRetryPolicy.GetNextAttemptUtc(retry, now));

        Assert.Throws<ArgumentOutOfRangeException>(() => CloudRetryPolicy.GetNextAttemptUtc(0, now));
        Assert.Throws<ArgumentOutOfRangeException>(() => CloudRetryPolicy.GetNextAttemptUtc(7, now));

        Assert.False(CloudRetryPolicy.IsAutomaticRetryLimitReached(expectedMinutes.Length - 1));
        Assert.True(CloudRetryPolicy.IsAutomaticRetryLimitReached(expectedMinutes.Length));
        Assert.True(CloudRetryPolicy.IsAutomaticRetryLimitReached(expectedMinutes.Length + 1));
    }

    [Fact]
    public async Task Queue_SurvivesStoreRecreation_AndCanBeCompleted()
    {
        var now = DateTime.UtcNow;
        await store.UpsertCloudRetryAsync(new CloudRetryQueueEntry
        {
            PlayniteId = "game-1", RetryCount = 2, NextAttemptUtc = now.AddMinutes(5),
            LastError = "temporary network failure", CreatedUtc = now, UpdatedUtc = now
        }, CancellationToken.None);

        var restarted = NewStore();
        await restarted.InitializeAsync(CancellationToken.None);
        var loaded = await restarted.GetCloudRetryAsync("game-1", CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.RetryCount);
        Assert.Equal("temporary network failure", loaded.LastError);

        await restarted.RemoveCloudRetryAsync("game-1", CancellationToken.None);
        Assert.Null(await restarted.GetCloudRetryAsync("game-1", CancellationToken.None));
    }

    [Fact]
    public async Task QueueSchema_IsAddedWithoutReplacingAnExistingDatabase()
    {
        var migrationRoot = Path.Combine(root, "pre-cloud-retry-schema");
        Directory.CreateDirectory(migrationRoot);
        var migrationOptions = new WorkerOptions
        {
            DataDirectory = migrationRoot,
            LudusaviBackupDirectory = Path.Combine(migrationRoot, "Saves"),
            MediaArchiveDirectory = Path.Combine(migrationRoot, "Media")
        };

        await using (var legacy = new SqliteConnection($"Data Source={migrationOptions.DatabasePath}"))
        {
            await legacy.OpenAsync();
            var command = legacy.CreateCommand();
            command.CommandText = "CREATE TABLE legacy_marker(marker TEXT NOT NULL); INSERT INTO legacy_marker(marker) VALUES ('keep');";
            await command.ExecuteNonQueryAsync();
        }

        var migrated = new SqliteStateStore(migrationOptions, NullLogger<SqliteStateStore>.Instance);
        await migrated.InitializeAsync(CancellationToken.None);
        var now = DateTime.UtcNow;
        await migrated.UpsertCloudRetryAsync(new CloudRetryQueueEntry
        {
            PlayniteId = "migrated-game", RetryCount = 1, NextAttemptUtc = now,
            LastError = "offline", CreatedUtc = now, UpdatedUtc = now
        }, CancellationToken.None);

        Assert.NotNull(await migrated.GetCloudRetryAsync("migrated-game", CancellationToken.None));
        await using var verification = new SqliteConnection($"Data Source={migrationOptions.DatabasePath}");
        await verification.OpenAsync();
        var preserved = verification.CreateCommand();
        preserved.CommandText = "SELECT marker FROM legacy_marker;";
        Assert.Equal("keep", await preserved.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Queue_OnlyReturnsDueEntries_AndDeferredEntryDoesNotSpin()
    {
        var now = DateTime.UtcNow;
        await store.UpsertCloudRetryAsync(new CloudRetryQueueEntry
        {
            PlayniteId = "due", RetryCount = 1, NextAttemptUtc = now.AddMinutes(-1),
            LastError = "timeout", CreatedUtc = now, UpdatedUtc = now
        }, CancellationToken.None);
        await store.UpsertCloudRetryAsync(new CloudRetryQueueEntry
        {
            PlayniteId = "later", RetryCount = 1, NextAttemptUtc = now.AddHours(1),
            LastError = "offline", CreatedUtc = now, UpdatedUtc = now
        }, CancellationToken.None);

        var due = await store.GetDueCloudRetriesAsync(now, 10, CancellationToken.None);
        Assert.Single(due);
        Assert.Equal("due", due[0].PlayniteId);

        await store.DeferCloudRetryAsync("due", now.AddMinutes(5), "configuration unavailable", CancellationToken.None);
        Assert.Empty(await store.GetDueCloudRetriesAsync(now, 10, CancellationToken.None));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private SqliteStateStore NewStore() => new(options, NullLogger<SqliteStateStore>.Instance);
}

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

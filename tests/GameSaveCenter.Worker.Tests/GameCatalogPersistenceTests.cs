using GameSaveCenter.Contracts;
using GameSaveCenter.Worker.Configuration;
using GameSaveCenter.Worker.Infrastructure;
using GameSaveCenter.Worker.Persistence;
using GameSaveCenter.Worker.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameSaveCenter.Worker.Tests;

public sealed class GameCatalogPersistenceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "GameSaveCenter.Tests", Guid.NewGuid().ToString("N"));
    private readonly WorkerOptions options;
    private readonly SqliteStateStore store;
    private readonly GameCatalogService catalog;

    public GameCatalogPersistenceTests()
    {
        options = new WorkerOptions
        {
            DataDirectory = root,
            LudusaviBackupDirectory = Path.Combine(root, "Saves"),
            MediaArchiveDirectory = Path.Combine(root, "Media"),
            LudusaviExecutable = Path.Combine(root, "missing-ludusavi.exe")
        };
        store = new SqliteStateStore(options, NullLogger<SqliteStateStore>.Instance);
        store.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        var runner = new ExternalProcessRunner(NullLogger<ExternalProcessRunner>.Instance);
        var ludusavi = new LudusaviClient(options, runner, NullLogger<LudusaviClient>.Instance);
        catalog = new GameCatalogService(store, ludusavi, NullLogger<GameCatalogService>.Instance);
    }

    [Fact]
    public async Task FullRefreshSkipsUnchangedRowsAndOnlyPersistsChangedDescriptors()
    {
        var original = new GameDescriptorDto
        {
            PlayniteId = "game-1",
            Name = "Example Game",
            Platform = GamePlatformKind.Steam,
            PlatformGameId = "123",
            IsInstalled = true
        };

        await catalog.UpsertAndMatchAsync(new[] { original }, CancellationToken.None);
        var firstTimestamp = await ReadUpdatedUtcAsync("game-1");

        await store.SetGameMatchAsync("game-1", "Ludusavi Example", 0.95,
            GameMatchInput.CreateHash(original), CancellationToken.None);
        firstTimestamp = await ReadUpdatedUtcAsync("game-1");

        await Task.Delay(25);
        await catalog.UpsertAndMatchAsync(new[] { original }, CancellationToken.None);
        var unchangedTimestamp = await ReadUpdatedUtcAsync("game-1");
        Assert.Equal(firstTimestamp, unchangedTimestamp);

        await Task.Delay(25);
        var changed = new GameDescriptorDto
        {
            PlayniteId = original.PlayniteId,
            Name = "Example Game (Updated)",
            Platform = original.Platform,
            PlatformGameId = original.PlatformGameId,
            IsInstalled = original.IsInstalled
        };
        await catalog.UpsertAndMatchAsync(new[] { changed }, CancellationToken.None);
        var changedTimestamp = await ReadUpdatedUtcAsync("game-1");
        Assert.NotEqual(unchangedTimestamp, changedTimestamp);

        var changedCache = await store.GetGameMatchCacheAsync(CancellationToken.None);
        Assert.Empty(changedCache["game-1"].LudusaviName);
        Assert.Null(changedCache["game-1"].LastMatchAttemptUtc);
    }

    private async Task<string?> ReadUpdatedUtcAsync(string playniteId)
    {
        await using var connection = new SqliteConnection($"Data Source={options.DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT updated_utc FROM games WHERE playnite_id=$id;";
        command.Parameters.AddWithValue("$id", playniteId);
        return await command.ExecuteScalarAsync() as string;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}

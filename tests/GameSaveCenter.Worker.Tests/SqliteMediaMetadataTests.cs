using GameSaveCenter.Contracts;
using GameSaveCenter.Worker.Configuration;
using GameSaveCenter.Worker.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameSaveCenter.Worker.Tests;

public sealed class SqliteMediaMetadataTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"GameSaveCenter.Tests",Guid.NewGuid().ToString("N"));
    private readonly SqliteStateStore store;

    public SqliteMediaMetadataTests()
    {
        var options=new WorkerOptions
        {
            DataDirectory=root,
            LudusaviBackupDirectory=Path.Combine(root,"Saves"),
            MediaArchiveDirectory=Path.Combine(root,"Media")
        };
        store=new SqliteStateStore(options,NullLogger<SqliteStateStore>.Instance);
        store.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task BatchMetadataUpdate_IsAtomicAndPreservesUnchangedFields()
    {
        await AddAsync("one",false,"first");
        await AddAsync("two",false,"second");

        await store.UpdateMediaMetadataBatchAsync(new MediaMetadataBatchUpdateDto
        {
            MediaIds=new List<string>{"one","two"},
            IsFavorite=true,
            UpdateComment=false,
            Comment="ignored"
        },CancellationToken.None);

        var one=await store.GetMediaByIdAsync("one",CancellationToken.None);
        var two=await store.GetMediaByIdAsync("two",CancellationToken.None);
        Assert.True(one!.IsFavorite);
        Assert.True(two!.IsFavorite);
        Assert.Equal("first",one.Comment);
        Assert.Equal("second",two.Comment);

        await Assert.ThrowsAsync<InvalidOperationException>(()=>store.UpdateMediaMetadataBatchAsync(new MediaMetadataBatchUpdateDto
        {
            MediaIds=new List<string>{"one","missing"},
            IsFavorite=false
        },CancellationToken.None));

        one=await store.GetMediaByIdAsync("one",CancellationToken.None);
        Assert.True(one!.IsFavorite);
    }

    [Fact]
    public async Task DeviceConflictDecision_IsPersistedWithoutFileOperations()
    {
        var decision=new DeviceConflictDecisionDto
        {
            PlayniteId="game",RemoteDevice="OTHER-PC",LocalBackupId="local",
            RemoteBackupId="remote",Decision="KeepBoth",Comment="manual review",DecidedUtc=DateTime.UtcNow
        };
        await store.SaveDeviceConflictDecisionAsync(decision,CancellationToken.None);
        var loaded=await store.GetDeviceConflictDecisionAsync("game","OTHER-PC",CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal("KeepBoth",loaded!.Decision);
        Assert.Equal("manual review",loaded.Comment);
    }

    [Fact]
    public async Task MediaSourceRule_CanBePausedAndDeletedWithoutTouchingArchivedMedia()
    {
        var source=new MediaSourceRuleDto
        {
            SourceId="custom-source",
            PlayniteId="game",
            SourceKind=MediaSourceKind.Custom,
            RootPath=root,
            IncludePattern="*.png",
            Enabled=true
        };
        await store.AddMediaSourceAsync(source,CancellationToken.None);
        source.Enabled=false;
        await store.AddMediaSourceAsync(source,CancellationToken.None);

        var paused=await store.GetMediaSourcesAsync("game",CancellationToken.None);
        Assert.Single(paused);
        Assert.False(paused[0].Enabled);

        await store.DeleteMediaSourceAsync(source.SourceId,CancellationToken.None);
        Assert.Empty(await store.GetMediaSourcesAsync("game",CancellationToken.None));
        Assert.True(Directory.Exists(root));
    }

    [Fact]
    public async Task UpsertedGameKeepsMatchAttemptUnsetUntilMatchingCompletes()
    {
        await store.UpsertGamesAsync(new[]
        {
            new GameDescriptorDto
            {
                PlayniteId="pending-game",
                Name="Pending Game",
                Platform=GamePlatformKind.Steam,
                PlatformGameId="123",
                IsInstalled=true
            }
        }, CancellationToken.None);

        var cache = await store.GetGameMatchCacheAsync(CancellationToken.None);
        Assert.True(cache.ContainsKey("pending-game"));
        Assert.Null(cache["pending-game"].LastMatchAttemptUtc);
        Assert.Empty(cache["pending-game"].LudusaviName);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if(Directory.Exists(root))Directory.Delete(root,true);
    }

    private Task AddAsync(string id,bool favorite,string comment)=>store.AddMediaAsync(new MediaItemDto
    {
        MediaId=id,
        PlayniteId="game",
        Kind=MediaKind.Screenshot,
        Source=MediaSourceKind.Custom,
        ArchivePath=Path.Combine(root,id+".png"),
        OriginalPath=Path.Combine(root,id+".png"),
        CapturedUtc=DateTime.UtcNow,
        SizeBytes=10,
        Sha256=id.PadRight(64,'0'),
        IsFavorite=favorite,
        Comment=comment,
        ClassificationState="Assigned"
    },CancellationToken.None);
}

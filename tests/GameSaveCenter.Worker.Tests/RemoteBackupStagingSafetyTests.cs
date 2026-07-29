using GameSaveCenter.Worker.Services;
using GameSaveCenter.Worker.Infrastructure;
using Xunit;

namespace GameSaveCenter.Worker.Tests;

public sealed class RemoteBackupStagingSafetyTests
{
    [Theory]
    [InlineData("DESKTOP-ABC")]
    [InlineData("SteamDeck")]
    [InlineData("客厅电脑")]
    public void DeviceName_AcceptsSingleSafeSegment(string value)
        =>Assert.True(RemoteBackupStagingService.IsSafeDeviceName(value));

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../other")]
    [InlineData(@"device\Saves")]
    [InlineData("device/Saves")]
    [InlineData("device:")]
    public void DeviceName_RejectsTraversalAndSeparators(string value)
        =>Assert.False(RemoteBackupStagingService.IsSafeDeviceName(value));

    [Fact]
    public void StagingId_OnlyAcceptsOpaqueLowercaseHex()
    {
        Assert.True(RemoteBackupStagingService.IsOpaqueId("0123456789abcdef0123456789abcdef"));
        Assert.False(RemoteBackupStagingService.IsOpaqueId("../0123456789abcdef0123456789abc"));
        Assert.False(RemoteBackupStagingService.IsOpaqueId("0123456789ABCDEF0123456789ABCDEF"));
        Assert.False(RemoteBackupStagingService.IsOpaqueId("0123456789abcdef"));
    }

    [Fact]
    public void DownloadChecksum_ChecksEveryRemoteFileAgainstLocalVault()
    {
        var arguments=RcloneClient.BuildChecksumCheckArguments("cloud:GameSaveCenter",@"DEVICE\Saves",@"C:\staging\Vault");
        Assert.Equal(new[]{"check","cloud:GameSaveCenter/DEVICE/Saves",@"C:\staging\Vault","--one-way"},arguments);
    }
}

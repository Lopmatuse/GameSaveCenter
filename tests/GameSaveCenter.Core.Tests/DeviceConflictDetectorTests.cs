using GameSaveCenter.Core.Models;
using GameSaveCenter.Core.Services;
using Xunit;
namespace GameSaveCenter.Core.Tests;
public sealed class DeviceConflictDetectorTests
{
    [Fact]
    public void MissingLocalSummaryIsNotAutoResolved()
    {
        var remote=new BackupSnapshot{BackupId="handheld",SourceDevice="HANDHELD",CreatedUtc=DateTime.UtcNow,TotalBytes=140};
        var conflict=new DeviceConflictDetector().Detect(null,remote);
        Assert.False(conflict.HasConflict);Assert.Equal("OnlyOneSideAvailable",conflict.Reason);
    }

    [Fact]
    public void DivergentDevicesAreNotAutoResolved()
    {
        var left=new BackupSnapshot{BackupId="desktop",SourceDevice="DESKTOP",CreatedUtc=DateTime.UtcNow,TotalBytes=100};
        var right=new BackupSnapshot{BackupId="handheld",SourceDevice="HANDHELD",CreatedUtc=DateTime.UtcNow.AddMinutes(5),TotalBytes=140};
        var conflict=new DeviceConflictDetector().Detect(left,right);
        Assert.True(conflict.HasConflict);Assert.True(string.IsNullOrEmpty(conflict.PreferredBackupId));
    }
}

using GameSaveCenter.Core.Models;
using GameSaveCenter.Core.Services;
using Xunit;
namespace GameSaveCenter.Core.Tests;
public sealed class RetentionPlannerTests
{
    [Fact]
    public void LockedAndPreRestoreVersionsAreAlwaysKept()
    {
        var now=DateTime.UtcNow;
        var versions=new[]{new BackupSnapshot{BackupId="locked",CreatedUtc=now.AddYears(-3),IsLocked=true},new BackupSnapshot{BackupId="pre",CreatedUtc=now.AddYears(-2),IsPreRestore=true}};
        var plan=new RetentionPlanner().CreatePlan(versions,new RetentionPolicy(),now);
        Assert.Contains(plan.Keep,x=>x.BackupId=="locked");Assert.Contains(plan.Keep,x=>x.BackupId=="pre");
    }
}

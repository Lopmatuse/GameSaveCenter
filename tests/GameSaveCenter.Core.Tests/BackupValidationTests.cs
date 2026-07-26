using GameSaveCenter.Core.Models;
using GameSaveCenter.Core.Services;
using Xunit;
namespace GameSaveCenter.Core.Tests;
public sealed class BackupValidationTests
{
    [Fact]
    public void EmptyBackupIsCritical()
    {
        var findings=new BackupValidationService().Validate(new BackupSnapshot(),null,null,true);
        Assert.Contains(findings,x=>x.Code=="EMPTY_BACKUP");
    }

    [Fact]
    public void LargeSizeDropIsDetected()
    {
        var previous=new BackupSnapshot{FileCount=10,TotalBytes=1000};
        var current=new BackupSnapshot{FileCount=10,TotalBytes=200};
        var findings=new BackupValidationService().Validate(current,previous,null,true);
        Assert.Contains(findings,x=>x.Code=="BACKUP_SIZE_DROP");
    }
}

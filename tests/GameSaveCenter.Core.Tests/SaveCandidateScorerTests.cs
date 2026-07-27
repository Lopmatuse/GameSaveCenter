using GameSaveCenter.Core.Services;
using Xunit;
namespace GameSaveCenter.Core.Tests;
public sealed class SaveCandidateScorerTests
{
    [Fact]
    public void XboxWgsAndRepeatedChangesScoreHighly()
    {
        var result=new SaveCandidateScorer().Score(@"C:\\Packages\\Game\\SystemAppData\\wgs",new[]{"slot1.sav","profile.dat","slot2.sav","state.json","save.bin"},true,true,true);
        Assert.True(result.Score>=0.8);Assert.Contains(result.Reasons,x=>x.Contains("WGS"));
    }

    [Fact]
    public void ConfigAndDatabaseFilesRemainValidCandidates()
    {
        var result=new SaveCandidateScorer().Score(@"C:\\Users\\Player\\AppData\\Local\\ExampleGame",new[]{"settings.ini","profile.db","world.sqlite"},true,false,false);
        Assert.True(result.Score>=0.5);Assert.Equal(3,result.SaveLikeExtensionCount);
    }
}

using GameSaveCenter.Core.Models;
using GameSaveCenter.Core.Services;
using Xunit;
namespace GameSaveCenter.Core.Tests;
public sealed class GameMatcherTests
{
    [Fact]
    public void PlatformIdWinsOverTitleNoise()
    {
        var matcher=new GameMatcher();
        var game=new GameProfile{Name="Persona 3 Reload",PlatformGameId="2161700"};
        var candidates=new[]{new LudusaviGameIdentity{Name="Unrelated title",PlatformIds={"2161700"}},new LudusaviGameIdentity{Name="Persona 3 Reload"}};
        var result=matcher.Match(game,candidates);
        Assert.Equal("Unrelated title",result.LudusaviName);Assert.True(result.Confidence>=0.99);
    }

    [Fact]
    public void EditionWordsDoNotBreakMatching()
    {
        var result=new GameMatcher().Match(new GameProfile{Name="Cyberpunk 2077 Ultimate Edition"},new[]{new LudusaviGameIdentity{Name="Cyberpunk 2077"}});
        Assert.Equal("Cyberpunk 2077",result.LudusaviName);Assert.True(result.Confidence>0.8);
    }
}

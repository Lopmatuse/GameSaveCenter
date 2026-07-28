using GameSaveCenter.Core.Services;
using Xunit;

namespace GameSaveCenter.Core.Tests;

public sealed class ArchivePathGuardTests
{
    [Fact]
    public void ResolveEntryPath_AllowsNestedFile()
    {
        var root=Path.Combine(Path.GetTempPath(),"gsc-archive-root");
        var result=ArchivePathGuard.ResolveEntryPath(root,Path.Combine("trainer","trainer.exe"));
        Assert.StartsWith(Path.GetFullPath(root),result,StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../escape.exe")]
    [InlineData("../../Windows/escape.exe")]
    public void ResolveEntryPath_RejectsTraversal(string entry)
    {
        var root=Path.Combine(Path.GetTempPath(),"gsc-archive-root");
        Assert.Throws<InvalidDataException>(()=>ArchivePathGuard.ResolveEntryPath(root,entry));
    }
}

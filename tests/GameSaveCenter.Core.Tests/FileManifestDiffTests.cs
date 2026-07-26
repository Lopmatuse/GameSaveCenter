using GameSaveCenter.Core.Models;
using GameSaveCenter.Core.Services;
using Xunit;
namespace GameSaveCenter.Core.Tests;
public sealed class FileManifestDiffTests
{
    [Fact]
    public void FindsAddedRemovedAndModifiedFiles()
    {
        var before=new[]{new FileManifestEntry{RelativePath="a.sav",SizeBytes=1,Sha256="a"},new FileManifestEntry{RelativePath="old.sav",SizeBytes=1,Sha256="o"}};
        var after=new[]{new FileManifestEntry{RelativePath="a.sav",SizeBytes=2,Sha256="b"},new FileManifestEntry{RelativePath="new.sav",SizeBytes=1,Sha256="n"}};
        var diff=new FileManifestDiffService().Compare(before,after);
        Assert.Single(diff.Modified);Assert.Single(diff.Removed);Assert.Single(diff.Added);
    }
}

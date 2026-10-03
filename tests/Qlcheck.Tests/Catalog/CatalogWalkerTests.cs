using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class CatalogWalkerTests
{
    [Fact]
    public void TryClass_returns_true_and_the_class_for_an_implemented_id()
    {
        var id = Assert.Single(CatalogWalker.ImplementedIds, i => i == "no-public-field");

        Assert.True(CatalogWalker.TryClass(id, out var group));
        Assert.Equal(CheckClass.Syntax, group);
    }

    [Fact]
    public void TryClass_returns_false_for_an_unknown_id()
    {
        Assert.False(CatalogWalker.TryClass("not-a-real-check", out _));
    }

    [Fact]
    public void TryClass_returns_compilation_for_unused_using()
    {
        Assert.True(CatalogWalker.TryClass("unused-using", out var group));
        Assert.Equal(CheckClass.Compilation, group);
        Assert.Contains("unused-using", CatalogWalker.ImplementedIds);
    }

    [Fact]
    public void TryClass_returns_process_for_coverage()
    {
        Assert.True(CatalogWalker.TryClass("coverage", out var group));
        Assert.Equal(CheckClass.Process, group);
        Assert.Contains("coverage", CatalogWalker.ImplementedIds);
    }
}

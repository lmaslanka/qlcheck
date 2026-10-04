using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class CheckDiscoveryTests
{
    [Fact]
    public void Discovers_migrated_file_checks_as_catalog_rows()
    {
        var checks = CheckDiscovery.All();
        Assert.Contains(checks, c => c.Id == "magic-literal" && c is CatalogCheck && c.EnabledByDefault);
        Assert.Contains(checks, c => c.Id == "string-concat" && c is CatalogCheck && c.EnabledByDefault);
        Assert.Contains(checks, c => c.Id == "string-empty" && c is CatalogCheck && c.EnabledByDefault);
        Assert.Contains(checks, c => c.Id == "switch-pattern" && c is CatalogCheck && c.EnabledByDefault);
        Assert.Contains(checks, c => c.Id == "one-type-per-file" && c is CatalogCheck && c.EnabledByDefault);
        Assert.Contains(checks, c => c.Id == "unused-using" && c is CatalogCheck && c.EnabledByDefault);
        Assert.Contains(checks, c => c.Id == CoverageCheck.CheckId && c is CatalogCheck && !c.EnabledByDefault);
    }

    [Fact]
    public void Default_selection_skips_coverage()
    {
        Assert.True(CheckDiscovery.TrySelect([], [], out var checks, new StringWriter()));
        Assert.DoesNotContain(checks, check => check.Id == CoverageCheck.CheckId);
    }
}

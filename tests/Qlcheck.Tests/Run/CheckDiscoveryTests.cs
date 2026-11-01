using Qlcheck.Checks;
using Qlcheck.Languages;
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

    [Fact]
    public void All_collects_the_checks_of_every_language_in_id_order()
    {
        var checks = CheckDiscovery.All([new StubLanguage("b", "zeta", "alpha"), new StubLanguage("a", "mid")]);

        Assert.Equal(["alpha", "mid", "zeta"], checks.Select(check => check.Id));
    }

    [Fact]
    public void All_rejects_an_id_owned_by_two_languages()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => CheckDiscovery.All([new StubLanguage("a", "same"), new StubLanguage("b", "same")]));

        Assert.Contains("same", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unimplemented_check_from_any_language_is_rejected_when_selected()
    {
        using var stderr = new StringWriter();

        Assert.False(CheckDiscovery.TrySelect(["architecture-tangle"], [], out _, stderr));
        Assert.Contains("has no implementation", stderr.ToString(), StringComparison.Ordinal);
    }

    private sealed class StubLanguage(string id, params string[] checkIds) : ILanguage
    {
        public string Id => id;

        public IReadOnlyList<ICheck> Checks => checkIds.Select(checkId => (ICheck)new StubCheck(checkId, id)).ToList();

        public bool Matches(string path) => false;

        public RunResult Execute(IReadOnlyList<SourceScan.LoadedSource> files, IReadOnlyList<ICheck> checks, RunInputs inputs) => new([], []);
    }

    private sealed record StubCheck(string Id, string Language) : ICheck;
}

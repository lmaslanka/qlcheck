using Qlcheck.Checks;
using Qlcheck.Languages.CSharp.Checks.Coverage;

namespace Qlcheck.Tests;

public class CSharpLanguageTests
{
    [Fact]
    public void Execute_aggregates_coverage_check_results()
    {
        var file = new SourceScan.LoadedSource("/repo/Repo.cs", new SourceFile("Repo.cs", "class C { }"));
        var language = new CSharpLanguage();

        var result = language.Execute([file], [new FakeCoverageCheck()]);

        Assert.Contains(result.Findings, finding => finding.Check == FakeCoverageCheck.Id);
        Assert.Single(result.Coverage);
    }

    private sealed class FakeCoverageCheck : ICoverageCheck
    {
        public const string Id = "fake-coverage";

        string ICheck.Id => Id;

        public string Language => CSharpLanguage.LanguageId;

        public CoverageAnalysis Analyze(IReadOnlyList<SourceScan.LoadedSource> files) =>
            new(
                [Finding.At(Id, "Repo.cs", 1, 1, "covered")],
                [new CoverageFile("Repo.cs", false, [])]);
    }
}

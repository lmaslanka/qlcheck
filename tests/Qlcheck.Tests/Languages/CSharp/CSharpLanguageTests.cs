using Qlcheck.Languages.CSharp.Catalog;
using Qlcheck.Languages.CSharp.Checks.Coverage;

namespace Qlcheck.Tests;

public class CSharpLanguageTests
{
    [Fact]
    public void Execute_aggregates_coverage_check_results()
    {
        var dir = Directory.CreateTempSubdirectory("qlcheck_csharplanguage_");
        try
        {
            var fullPath = Path.Combine(dir.FullName, "Repo.cs");
            var file = new SourceScan.LoadedSource(fullPath, new SourceFile("Repo.cs", "class C { }"));
            var language = new CSharpLanguage(_ => new CoverageAnalysis(
                [Finding.At("coverage", "Repo.cs", 1, 1, "covered")],
                [new CoverageFile("Repo.cs", false, [])]));
            var coverage = Catalog.Checks.Single(c => c.Id == "coverage");

            var result = language.Execute([file], [coverage]);

            Assert.Contains(result.Findings, finding => finding.Check == "coverage");
            Assert.Single(result.Coverage);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}

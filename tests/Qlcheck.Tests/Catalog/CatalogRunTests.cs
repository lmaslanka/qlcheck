using Microsoft.CodeAnalysis;
using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class CatalogRunTests
{
    [Fact]
    public void Execute_compiles_against_the_owning_project_when_csproj_is_known()
    {
        var dir = Directory.CreateTempSubdirectory("qlcheck_catalogrun_");
        try
        {
            var csproj = Path.Combine(dir.FullName, "Sample.csproj");
            File.WriteAllText(csproj, "<Project></Project>");
            var repoPath = Path.Combine(dir.FullName, "Repo.cs");
            var source = "class C { public int Field; }";
            var file = new SourceFile("Repo.cs", source);
            var tree = CSharpTrees.Parse(file);
            var loaded = new SourceScan.LoadedSource(repoPath, file);
            var trees = new Dictionary<string, SyntaxTree> { [repoPath] = tree };
            var checks = new List<CatalogCheck>
            {
                Catalog.Checks.First(check => check.Id == "no-public-field"),
            };

            var findings = CatalogRun.Execute([loaded], csproj, trees, checks);

            Assert.Contains(findings, finding => finding.Check == "no-public-field");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}

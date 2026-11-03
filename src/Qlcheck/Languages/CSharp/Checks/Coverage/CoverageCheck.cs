using Qlcheck.Scan;

namespace Qlcheck.Languages.CSharp.Checks.Coverage;

internal sealed class CoverageCheck
{
    internal const string CheckId = "coverage";

    internal const string UncoveredMessage = "Uncovered.";

    internal const string MissingMessage = "Not in coverage result.";

    internal const string NoProductMessage = "No product source to cover.";

    public CoverageAnalysis Analyze(IReadOnlyList<SourceScan.LoadedSource> files)
    {
        var product = ProductFiles(files);
        var hits = new LcovMap.Document();
        foreach (var group in product.GroupBy(file => CSharpCompilations.FindCsproj(file.FullPath)!))
        {
            var include = CsprojReader.IncludeFilter(group.Key);
            foreach (var test in TestProjectFinder.Find(group.Key))
            {
                hits.Merge(LcovMap.Parse(CoverageRunner.Collect(test, group.Key, include)));
            }
        }

        return CoverageMap.Build(product, hits);
    }

    private static List<SourceScan.LoadedSource> ProductFiles(IReadOnlyList<SourceScan.LoadedSource> files)
    {
        var product = new List<SourceScan.LoadedSource>();
        foreach (var file in files)
        {
            if (GeneratedFiles.IsGenerated(file.File.Path, file.File.Text))
            {
                continue;
            }

            var csproj = CSharpCompilations.FindCsproj(file.FullPath);
            if (csproj is null)
            {
                throw new InvalidOperationException($"No project for {file.File.Path}.");
            }

            if (CsprojReader.Read(csproj).IsTestProject)
            {
                continue;
            }

            product.Add(file);
        }

        if (product.Count == 0)
        {
            throw new InvalidOperationException(NoProductMessage);
        }

        return product;
    }
}

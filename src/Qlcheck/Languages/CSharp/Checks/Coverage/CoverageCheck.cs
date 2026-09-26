using Qlcheck.Scan;

namespace Qlcheck.Languages.CSharp.Checks.Coverage;

internal sealed class CoverageCheck : ICoverageCheck
{
    public const string CheckId = "coverage";

    public const string UncoveredMessage = "Uncovered.";

    public const string MissingMessage = "Not in coverage result.";

    public const string NoProductMessage = "No product source to cover.";

    private const string GeneratedSuffix = ".g.cs";

    private const string DesignerSuffix = ".Designer.cs";

    public string Id => CheckId;

    public string Language => CSharpLanguage.LanguageId;

    public bool EnabledByDefault => false;

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
            if (IsGenerated(file.File.Path))
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

    private static bool IsGenerated(string path) =>
        path.EndsWith(GeneratedSuffix, StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(DesignerSuffix, StringComparison.OrdinalIgnoreCase);
}

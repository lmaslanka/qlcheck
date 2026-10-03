// Copyright (c) qlcheck contributors.
using Qlcheck.Checks;
using Qlcheck.Languages.CSharp.Catalog;
using Qlcheck.Languages.CSharp.Checks.Coverage;
using Qlcheck.Scan;

namespace Qlcheck.Languages.CSharp;

internal sealed class CSharpLanguage : ILanguage
{
    internal const string LanguageId = "csharp";

    private const string Extension = ".cs";

    private readonly Func<IReadOnlyList<SourceScan.LoadedSource>, CoverageAnalysis> _analyzeCoverage;

    public CSharpLanguage()
        : this(files => new CoverageCheck().Analyze(files))
    {
    }

    internal CSharpLanguage(Func<IReadOnlyList<SourceScan.LoadedSource>, CoverageAnalysis> analyzeCoverage)
    {
        _analyzeCoverage = analyzeCoverage;
    }

    public string Id => LanguageId;

    public bool Matches(string path) =>
        path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

    public RunResult Execute(
        IReadOnlyList<SourceScan.LoadedSource> loaded,
        IReadOnlyList<ICheck> checks)
    {
        foreach (var check in checks)
        {
            if (check is not CatalogCheck)
            {
                throw new InvalidOperationException($"Check '{check.Id}' is not a C# check.");
            }
        }

        var findings = RunCatalog(loaded, checks);
        var coverage = new List<CoverageFile>();
        if (checks.Any(check => check.Id == CoverageCheck.CheckId))
        {
            var analysis = _analyzeCoverage(loaded);
            findings.AddRange(analysis.Findings);
            coverage.AddRange(analysis.Files);
        }

        return new RunResult(findings, coverage);
    }

    private static List<Finding> RunCatalog(
        IReadOnlyList<SourceScan.LoadedSource> loaded,
        IReadOnlyList<ICheck> checks)
    {
        var catalog = checks.OfType<CatalogCheck>().ToList();
        var findings = new List<Finding>();
        if (catalog.Count == 0)
        {
            return findings;
        }

        var trees = loaded.ToDictionary(source => source.FullPath, source => CSharpTrees.Parse(source.File));
        var included = loaded
            .Select(source => source.File.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var group in loaded.GroupBy(source => CSharpCompilations.FindCsproj(source.FullPath) ?? string.Empty))
        {
            var csproj = string.IsNullOrEmpty(group.Key) ? null : group.Key;
            findings.AddRange(CatalogRun.Execute(group, csproj, trees, catalog, included));
        }

        return findings;
    }
}

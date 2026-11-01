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

    public IReadOnlyList<ICheck> Checks => Catalog.Catalog.Checks;

    public bool Matches(string path) =>
        path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

    public RunResult Execute(
        IReadOnlyList<SourceScan.LoadedSource> loaded,
        IReadOnlyList<ICheck> checks,
        RunInputs inputs)
    {
        foreach (var check in checks)
        {
            if (check is not CatalogCheck)
            {
                throw new InvalidOperationException($"Check '{check.Id}' is not a C# check.");
            }
        }

        var findings = RunCatalog(loaded, checks, inputs.Architecture);
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
        IReadOnlyList<ICheck> checks,
        ArchitectureFiles architecture)
    {
        var catalog = checks.OfType<CatalogCheck>().ToList();
        var findings = new List<Finding>();
        if (catalog.Count == 0)
        {
            return findings;
        }

        // Dependencies are extracted only when an architecture Check runs and a file declares one (D4).
        var selected = catalog.Select(check => check.Id).ToHashSet(StringComparer.Ordinal);
        var dependencies = ArchitectureEngine.Selects(selected) && architecture.Files.Count > 0 ? new List<GovernedDependency>() : null;
        var trees = loaded.ToDictionary(source => source.FullPath, source => CSharpTrees.Parse(source.File));
        foreach (var group in loaded.GroupBy(source => CSharpCompilations.FindCsproj(source.FullPath) ?? string.Empty))
        {
            var csproj = string.IsNullOrEmpty(group.Key) ? null : group.Key;
            findings.AddRange(CatalogRun.Execute(group, csproj, trees, catalog, architecture, dependencies is null ? null : dependencies.AddRange));
        }

        findings.AddRange(CatalogRun.ExecuteSourceSet(loaded, trees, catalog));
        if (dependencies is not null)
        {
            findings.AddRange(ArchitectureEngine.Apply(dependencies, selected, catalog.ToDictionary(check => check.Id, check => check.Message, StringComparer.Ordinal)));
        }

        return findings;
    }
}

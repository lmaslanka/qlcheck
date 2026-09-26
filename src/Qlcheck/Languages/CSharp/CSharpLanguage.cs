// Copyright (c) qlcheck contributors.
using Qlcheck.Checks;
using Qlcheck.Languages.CSharp.Catalog;
using Qlcheck.Languages.CSharp.Checks.Compilation;
using Qlcheck.Languages.CSharp.Checks.Coverage;
using Qlcheck.Languages.CSharp.Checks.File;
using Qlcheck.Scan;

namespace Qlcheck.Languages.CSharp;

internal sealed class CSharpLanguage : ILanguage
{
    internal const string LanguageId = "csharp";

    private const string Extension = ".cs";

    private const string AdhocCompilationName = "adhoc";

    public string Id => LanguageId;

    public bool Matches(string path) =>
        path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

    public RunResult Execute(
        IReadOnlyList<SourceScan.LoadedSource> loaded,
        IReadOnlyList<ICheck> checks)
    {
        foreach (var check in checks)
        {
            if (check is not IFileCheck and not ICompilationCheck and not ICoverageCheck and not CatalogCheck)
            {
                throw new InvalidOperationException($"Check '{check.Id}' is not a C# check.");
            }
        }

        var fileChecks = checks.OfType<IFileCheck>().ToList();
        var compilationChecks = checks.OfType<ICompilationCheck>().ToList();
        var findings = new List<Finding>();
        if (fileChecks.Count > 0 || compilationChecks.Count > 0)
        {
            findings.AddRange(RunSyntax(loaded, fileChecks, compilationChecks));
        }

        findings.AddRange(RunCatalog(loaded, checks));
        var coverage = new List<CoverageFile>();
        foreach (var check in checks.OfType<ICoverageCheck>())
        {
            var analysis = check.Analyze(loaded);
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
        foreach (var group in loaded.GroupBy(source => CSharpCompilations.FindCsproj(source.FullPath) ?? string.Empty))
        {
            var csproj = string.IsNullOrEmpty(group.Key) ? null : group.Key;
            findings.AddRange(CatalogRun.Execute(group, csproj, trees, catalog));
        }

        return findings;
    }

    private static List<Finding> RunSyntax(
        IReadOnlyList<SourceScan.LoadedSource> loaded,
        IReadOnlyList<IFileCheck> fileChecks,
        IReadOnlyList<ICompilationCheck> compilationChecks)
    {
        var parsed = loaded
            .Select(source => (source, Tree: CSharpTrees.Parse(source.File)))
            .ToList();
        var findings = parsed
            .AsParallel()
            .AsOrdered()
            .SelectMany(item => fileChecks.SelectMany(check => check.Analyze(item.source.File, item.Tree)))
            .ToList();
        if (compilationChecks.Count == 0)
        {
            return findings;
        }

        var included = parsed
            .Select(item => item.source.File.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var trees = parsed.ToDictionary(item => item.source.FullPath, item => item.Tree);
        foreach (var group in loaded.GroupBy(source => CSharpCompilations.FindCsproj(source.FullPath) ?? string.Empty))
        {
            var name = string.IsNullOrEmpty(group.Key)
                ? AdhocCompilationName
                : Path.GetFileNameWithoutExtension(group.Key);
            var compilation = CSharpCompilations.Create(name, group.Select(source => trees[source.FullPath]));
            foreach (var check in compilationChecks)
            {
                findings.AddRange(check.AnalyzeCompilation(compilation, included));
            }
        }

        return findings;
    }
}

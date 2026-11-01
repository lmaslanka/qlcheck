// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Qlcheck.Scan;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class CatalogRun
{
    private const string AdhocCompilationName = "adhoc";

    public static IReadOnlyList<Finding> Execute(
        IEnumerable<SourceScan.LoadedSource> group,
        string? csproj,
        IReadOnlyDictionary<string, SyntaxTree> trees,
        IReadOnlyList<CatalogCheck> checks)
    {
        var sources = group.ToList();
        var compilation = Compile(sources, csproj, trees);
        var selected = checks.Select(check => check.Id).ToHashSet(StringComparer.Ordinal);
        var messages = checks.ToDictionary(check => check.Id, check => check.Message, StringComparer.Ordinal);
        var findings = new List<Finding>();
        foreach (var source in sources)
        {
            var tree = trees[source.FullPath];
            var model = compilation.GetSemanticModel(tree);
            findings.AddRange(CatalogWalker.Walk(source.File, tree, model, selected, messages));
        }

        var reportable = sources.Select(source => trees[source.FullPath]).ToHashSet();
        var annotated = string.IsNullOrEmpty(csproj) || ProjectSettings.Load(csproj).HasNullableAnnotatedTarget;
        var compilationCtx = new CompilationContext(compilation, reportable, messages, annotated);
        CompilationEngine.Apply(compilationCtx, selected);
        findings.AddRange(compilationCtx.Findings);

        return findings;
    }

    // The run-wide Checks, once per run over every loaded file, across Compilations (plan 04, D3).
    public static IReadOnlyList<Finding> ExecuteSourceSet(
        IEnumerable<SourceScan.LoadedSource> loaded,
        IReadOnlyDictionary<string, SyntaxTree> trees,
        IReadOnlyList<CatalogCheck> checks)
    {
        var selected = checks.Select(check => check.Id).ToHashSet(StringComparer.Ordinal);
        if (!SourceSetEngine.Rows.Any(row => selected.Contains(row.Id)))
        {
            return [];
        }

        var messages = checks.ToDictionary(check => check.Id, check => check.Message, StringComparer.Ordinal);
        var ctx = new SourceSetContext(loaded.Select(source => new SourceSetFile(source.File.Path, trees[source.FullPath])), messages);
        SourceSetEngine.Apply(ctx, selected);
        return ctx.Findings;
    }

    private static Compilation Compile(
        IReadOnlyList<SourceScan.LoadedSource> sources,
        string? csproj,
        IReadOnlyDictionary<string, SyntaxTree> trees)
    {
        var name = string.IsNullOrEmpty(csproj)
            ? AdhocCompilationName
            : Path.GetFileNameWithoutExtension(csproj);
        var syntax = sources.Select(source => trees[source.FullPath]);
        if (string.IsNullOrEmpty(csproj))
        {
            return CSharpCompilations.Create(name, syntax);
        }

        return CSharpCompilations.CreateForProject(name, csproj, syntax);
    }
}

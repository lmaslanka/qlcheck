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

        return findings;
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

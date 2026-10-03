// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class UnusedUsingPattern
{
    private const string UnusedUsingDiagnostic = "CS8019";

    private static readonly HashSet<string> UnresolvedDiagnosticIds = new(StringComparer.Ordinal)
    {
        "CS0103", "CS0246", "CS1061",
    };

    public static void Apply(CompilationContext ctx, string id)
    {
        var models = new Dictionary<SyntaxTree, SemanticModel>();
        var diagnostics = ctx.Compilation.GetDiagnostics();
        var unresolvedTrees = diagnostics
            .Where(d => UnresolvedDiagnosticIds.Contains(d.Id))
            .Select(d => d.Location.SourceTree)
            .Where(t => t is not null)
            .ToHashSet();

        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Id != UnusedUsingDiagnostic || diagnostic.Location.SourceTree is null)
            {
                continue;
            }

            if (unresolvedTrees.Contains(diagnostic.Location.SourceTree))
            {
                continue;
            }

            if (!UsingResolved(ctx.Compilation, models, diagnostic))
            {
                continue;
            }

            var path = diagnostic.Location.SourceTree.FilePath.Replace('\\', '/');
            if (ctx.IncludedFiles.Count > 0
                && !ctx.IncludedFiles.Contains(path)
                && !ctx.IncludedFiles.Contains(diagnostic.Location.SourceTree.FilePath))
            {
                continue;
            }

            ctx.Report(id, path, diagnostic.Location, string.Empty);
        }
    }

    private static bool UsingResolved(
        Compilation compilation,
        Dictionary<SyntaxTree, SemanticModel> models,
        Diagnostic diagnostic)
    {
        var tree = diagnostic.Location.SourceTree;
        if (tree is null)
        {
            return false;
        }

        var directive = tree.GetRoot()
            .FindNode(diagnostic.Location.SourceSpan)
            .FirstAncestorOrSelf<UsingDirectiveSyntax>();
        if (directive?.Name is null)
        {
            return false;
        }

        if (!models.TryGetValue(tree, out var model))
        {
            model = compilation.GetSemanticModel(tree);
            models[tree] = model;
        }

        return model.GetSymbolInfo(directive.Name).Symbol is not null;
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck;

public sealed class UnusedUsingCheck : ICompilationCheck
{
    public const string CheckId = "unused-using";

    public const string Message = "Remove unused using directive.";

    private const string UnusedUsingDiagnostic = "CS8019";

    private static readonly HashSet<string> UnresolvedDiagnosticIds = new(StringComparer.Ordinal)
    {
        "CS0103", "CS0246", "CS1061",
    };

    public string Id => CheckId;

    public IReadOnlyList<Finding> AnalyzeCompilation(
        Compilation compilation,
        IReadOnlySet<string> includedFiles)
    {
        var findings = new List<Finding>();
        var models = new Dictionary<SyntaxTree, SemanticModel>();
        var diagnostics = compilation.GetDiagnostics();
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

            if (!UsingResolved(compilation, models, diagnostic))
            {
                continue;
            }

            var path = diagnostic.Location.SourceTree.FilePath.Replace('\\', '/');
            if (includedFiles.Count > 0 &&
                !includedFiles.Contains(path) &&
                !includedFiles.Contains(diagnostic.Location.SourceTree.FilePath))
            {
                continue;
            }

            findings.Add(Finding.At(CheckId, path, diagnostic.Location, Message, string.Empty));
        }

        return findings;
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

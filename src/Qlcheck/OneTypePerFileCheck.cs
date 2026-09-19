using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck;

public sealed class OneTypePerFileCheck : ICheck
{
    public const string CheckId = "one-type-per-file";

    public string Id => CheckId;

    public IReadOnlyList<Finding> Analyze(SourceFile file, SyntaxTree tree)
    {
        if (file.Path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
            file.Path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var types = tree.GetRoot()
            .DescendantNodes()
            .OfType<BaseTypeDeclarationSyntax>()
            .Where(IsTopLevel)
            .Where(t => !t.Modifiers.Any(SyntaxKind.FileKeyword))
            .ToList();

        var unique = new List<BaseTypeDeclarationSyntax>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in types)
        {
            if (names.Add(type.Identifier.Text))
            {
                unique.Add(type);
            }
        }

        if (unique.Count < 2)
        {
            return [];
        }

        var path = file.Path.Replace('\\', '/');
        var findings = new List<Finding>();
        foreach (var extra in unique.Skip(1))
        {
            var span = extra.Identifier.GetLocation().GetLineSpan().StartLinePosition;
            findings.Add(new Finding(
                Id: $"{CheckId}:{path}:{span.Line + 1}:{span.Character + 1}",
                Check: CheckId,
                File: path,
                Line: span.Line + 1,
                Column: span.Character + 1,
                Message: $"Move '{extra.Identifier.Text}' into its own file."));
        }

        return findings;
    }

    private static bool IsTopLevel(BaseTypeDeclarationSyntax type) =>
        type.Parent is CompilationUnitSyntax
            or NamespaceDeclarationSyntax
            or FileScopedNamespaceDeclarationSyntax;
}

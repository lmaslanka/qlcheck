using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Checks.File;

public sealed class OneTypePerFileCheck : IFileCheck
{
    private const string CheckIdValue = "one-type-per-file";

    private const string GeneratedSuffix = ".g.cs";

    private const string DesignerSuffix = ".Designer.cs";

    private const int MinTopLevelTypes = 2;

    public static string CheckId => CheckIdValue;

    public string Id => CheckId;

    public string Language => CSharpLanguage.LanguageId;

    public IReadOnlyList<Finding> Analyze(SourceFile file, SyntaxTree tree)
    {
        if (file.Path.EndsWith(GeneratedSuffix, StringComparison.OrdinalIgnoreCase)
            || file.Path.EndsWith(DesignerSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var types = tree.GetRoot()
            .DescendantNodes()
            .OfType<BaseTypeDeclarationSyntax>()
            .Where(IsTopLevel)
            .Where(t => !t.Modifiers.Any(SyntaxKind.FileKeyword))
            .ToList();

        var names = new HashSet<string>(StringComparer.Ordinal);
        var unique = types.Where(type => names.Add(type.Identifier.Text)).ToList();

        if (unique.Count < MinTopLevelTypes)
        {
            return [];
        }

        return unique.Skip(1)
            .Select(extra => Finding.At(
                CheckId,
                file.Path,
                extra.Identifier,
                $"Move '{extra.Identifier.Text}' into its own file."))
            .ToList();
    }

    private static bool IsTopLevel(BaseTypeDeclarationSyntax type) =>
        type.Parent is CompilationUnitSyntax
            or NamespaceDeclarationSyntax
            or FileScopedNamespaceDeclarationSyntax;
}

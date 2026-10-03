// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class OneTypePerFilePattern
{
    private const string GeneratedSuffix = ".g.cs";

    private const string DesignerSuffix = ".Designer.cs";

    private const int MinTopLevelTypes = 2;

    public static void Apply(WalkContext ctx, string id)
    {
        if (ctx.Path.EndsWith(GeneratedSuffix, StringComparison.OrdinalIgnoreCase)
            || ctx.Path.EndsWith(DesignerSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var types = ctx.Tree.GetRoot()
            .DescendantNodes()
            .OfType<BaseTypeDeclarationSyntax>()
            .Where(IsTopLevel)
            .Where(t => !t.Modifiers.Any(SyntaxKind.FileKeyword))
            .ToList();

        var names = new HashSet<string>(StringComparer.Ordinal);
        var unique = types.Where(type => names.Add(type.Identifier.Text)).ToList();

        if (unique.Count < MinTopLevelTypes)
        {
            return;
        }

        foreach (var extra in unique.Skip(1))
        {
            ctx.ReportCustom(id, extra.Identifier, $"Move '{extra.Identifier.Text}' into its own file.");
        }
    }

    private static bool IsTopLevel(BaseTypeDeclarationSyntax type) =>
        type.Parent is CompilationUnitSyntax
            or NamespaceDeclarationSyntax
            or FileScopedNamespaceDeclarationSyntax;
}

// Copyright (c) qlcheck contributors.
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Catalog;

// The compiler's own nullable warnings as Findings, only where `dotnet build` would give them: a
// reportable tree with no compile errors, in a project whose targets carry nullable annotations.
// Regions where nullable warnings are off give nothing, because the compiler gives nothing there.
internal static class NullablePattern
{
    private const string UninitializedId = "CS8618";

    private const string TextSeparator = "; ";

    public static void Apply(CompilationContext ctx, string id, params string[] compilerIds)
    {
        if (!ctx.HasNullableAnnotatedTarget)
        {
            return;
        }

        var groups = ctx.Diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning && compilerIds.Contains(diagnostic.Id))
            .Where(diagnostic => diagnostic.Location.SourceTree is { } tree && ctx.Includes(tree) && !ctx.ErrorTrees.Contains(tree))
            .Where(diagnostic => diagnostic.Id != UninitializedId || !InPartialType(diagnostic))
            .GroupBy(diagnostic => (diagnostic.Location.SourceTree!, diagnostic.Location.SourceSpan.Start));

        // CS8618 is given once per member at a constructor, so one location can carry several
        // warnings; they become one Finding whose message keeps every member's text.
        foreach (var group in groups)
        {
            var first = group.First();
            var path = CompilationContext.PathOf(first.Location.SourceTree!);
            ctx.ReportCustom(id, path, first.Location, $"{ctx.Message(id)} ({CompilerText(group.ToList())})");
        }
    }

    private static string CompilerText(List<Diagnostic> diagnostics) =>
        string.Join(
            TextSeparator,
            diagnostics.GroupBy(diagnostic => diagnostic.Id).Select(group =>
                $"{group.Key}: {string.Join(TextSeparator, group.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)))}"));

    // Another part of a partial type, possibly not scanned, may initialize the member.
    private static bool InPartialType(Diagnostic diagnostic)
    {
        var tree = diagnostic.Location.SourceTree!;
        var node = tree.GetRoot().FindNode(diagnostic.Location.SourceSpan);
        return node.FirstAncestorOrSelf<TypeDeclarationSyntax>()?.Modifiers.Any(SyntaxKind.PartialKeyword) == true;
    }
}

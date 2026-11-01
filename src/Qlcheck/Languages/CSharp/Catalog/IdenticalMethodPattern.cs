// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Catalog;

// identical-method (D1): a clone group whose every copy is the whole body of a method, accessor or
// local function. One Finding per copy, naming the other members; no copy is also duplicate-code.
internal static class IdenticalMethodPattern
{
    public static bool IsIdentical(CloneGroup group) => group.Copies.All(copy => copy.WholeBodyOf is not null);

    public static void Apply(SourceSetContext ctx, string id)
    {
        foreach (var group in ctx.Clones.Where(IsIdentical))
        {
            foreach (var copy in group.Copies)
            {
                var others = string.Join(", ", group.Copies.Where(other => other != copy).Select(other => $"`{NameOf(other.WholeBodyOf!)}` ({other})"));
                ctx.ReportCustom(id, copy.Path, copy.Location, $"{ctx.Message(id)} `{NameOf(copy.WholeBodyOf!)}` has the same body as {others}; make one call the other.");
            }
        }
    }

    private static string NameOf(SyntaxNode member) =>
        member switch
        {
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
            LocalFunctionStatementSyntax function => function.Identifier.ValueText,
            AccessorDeclarationSyntax { Parent.Parent: BasePropertyDeclarationSyntax property } accessor =>
                $"{(property as PropertyDeclarationSyntax)?.Identifier.ValueText ?? "this[]"}.{accessor.Keyword.ValueText}",
            _ => member.Kind().ToString(),
        };
}

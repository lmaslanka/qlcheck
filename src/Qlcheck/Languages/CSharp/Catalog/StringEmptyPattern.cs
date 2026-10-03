// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class StringEmptyPattern
{
    private const string ReplacementValue = "string.Empty";

    public static void Apply(WalkContext ctx, string id)
    {
        foreach (var literal in ctx.Nodes(SyntaxKind.StringLiteralExpression).OfType<LiteralExpressionSyntax>())
        {
            if (literal.Token.ValueText.Length != 0 || IsConstantContext(literal))
            {
                continue;
            }

            ctx.ReportWith(id, literal, ReplacementValue);
        }
    }

    private static bool IsConstantContext(LiteralExpressionSyntax literal)
    {
        for (var node = literal.Parent; node is not null; node = node.Parent)
        {
            var constant = node switch
            {
                AttributeSyntax or ParameterSyntax or CaseSwitchLabelSyntax or ConstantPatternSyntax => true,
                LocalDeclarationStatementSyntax local => local.Modifiers.Any(SyntaxKind.ConstKeyword),
                FieldDeclarationSyntax fieldDeclaration => fieldDeclaration.Modifiers.Any(SyntaxKind.ConstKeyword),
                _ => (bool?)null,
            };
            if (constant is bool result)
            {
                return result;
            }
        }

        return false;
    }
}

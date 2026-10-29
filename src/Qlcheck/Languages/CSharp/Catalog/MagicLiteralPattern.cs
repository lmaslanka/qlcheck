// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class MagicLiteralPattern
{
    public static void Apply(WalkContext ctx, string id)
    {
        var groups = ctx.Tree.GetRoot()
            .DescendantNodes()
            .OfType<LiteralExpressionSyntax>()
            .Where(LiteralContext.IsCandidate)
            .GroupBy(literal => (literal.Kind(), literal.Token.Value));

        foreach (var group in groups)
        {
            var literals = group.ToList();
            if (literals.Count < Thresholds.MagicLiteralRepeats)
            {
                continue;
            }

            foreach (var literal in literals)
            {
                ctx.ReportCustom(id, literal, Message(literal, literals.Count));
            }
        }
    }

    public static string NumberMessage(string literal, int count) =>
        $"Magic number {literal} is used {count} times in this file; make it a named const or enum member.";

    public static string StringMessage(string literal, int count) =>
        $"Magic string \"{literal}\" is used {count} times in this file; make it a named const.";

    private static string Message(LiteralExpressionSyntax literal, int count) =>
        literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? StringMessage(LiteralContext.Truncate(literal.Token.ValueText), count)
            : NumberMessage(literal.Token.Text, count);
}

// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class IdentMatch
{
    public static void Report(WalkContext ctx, string id, string name)
    {
        foreach (var token in ctx.Tree.GetRoot().DescendantTokens())
        {
            if (token.IsKind(SyntaxKind.IdentifierToken) && Same(token.Text, name))
            {
                ctx.Report(id, token);
            }
        }
    }

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.Ordinal);
}

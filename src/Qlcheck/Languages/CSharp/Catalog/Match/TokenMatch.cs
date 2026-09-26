// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog;

internal static class TokenMatch
{
    public static void Report(WalkContext ctx, string id, string text)
    {
        foreach (var token in ctx.Tree.GetRoot().DescendantTokens())
        {
            if (token.Text == text)
            {
                ctx.Report(id, token);
            }
        }
    }
}

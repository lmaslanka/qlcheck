// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class TriviaMatch
{
    public static void Report(WalkContext ctx, string id, string word)
    {
        foreach (var token in ctx.Tree.GetRoot().DescendantTokens(descendIntoTrivia: true))
        {
            ReportTrivia(ctx, id, word, token.LeadingTrivia);
            ReportTrivia(ctx, id, word, token.TrailingTrivia);
        }
    }

    private static void ReportTrivia(WalkContext ctx, string id, string word, SyntaxTriviaList trivia)
    {
        foreach (var item in trivia)
        {
            if (!IsComment(item))
            {
                continue;
            }

            if (item.ToString().Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                ctx.Report(id, item.GetLocation());
            }
        }
    }

    private static bool IsComment(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia);
}

// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Qlcheck.Languages.CSharp.Catalog.Queries;

internal static class FileFacts
{
    private const int EmptyCommentLimit = 4;

    private const char Newline = '\n';

    private const char Tab = '\t';

    public static bool MissingNewline(string text) => text.Length == 0 || text[^1] != Newline;

    public static bool HasTab(string text) => text.Contains(Tab);

    public static bool IsCodeComment(SyntaxTrivia trivia)
    {
        if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
        {
            return false;
        }

        var text = trivia.ToString();
        return text.Contains(';') && HasKeyword(text);
    }

    public static bool IsEmptyComment(SyntaxTrivia trivia)
    {
        if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
        {
            return false;
        }

        var text = trivia.ToString().Trim();
        return text.Length <= EmptyCommentLimit && !text.Contains(' ', StringComparison.Ordinal);
    }

    private const string ReturnWord = "return";

    private const string ClassWord = "class";

    private const string IfWord = "if";

    private static bool HasKeyword(string text) =>
        text.Contains(ReturnWord, StringComparison.Ordinal)
        || text.Contains(ClassWord, StringComparison.Ordinal)
        || text.Contains(IfWord, StringComparison.Ordinal);
}

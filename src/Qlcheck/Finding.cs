using Microsoft.CodeAnalysis;

namespace Qlcheck;

public sealed record Finding(
    string Id,
    string Check,
    string File,
    int Line,
    int Column,
    string Message,
    string? Replacement = null)
{
    public static Finding At(
        string checkId,
        string path,
        Location location,
        string message,
        string? replacement = null)
    {
        var file = path.Replace('\\', '/');
        var span = location.GetLineSpan().StartLinePosition;
        var line = span.Line + 1;
        var column = span.Character + 1;
        return new Finding(
            Id: $"{checkId}:{file}:{line}:{column}",
            Check: checkId,
            File: file,
            Line: line,
            Column: column,
            Message: message,
            Replacement: replacement);
    }

    public static Finding At(
        string checkId,
        string path,
        SyntaxNode node,
        string message,
        string? replacement = null) =>
        At(checkId, path, node.GetLocation(), message, replacement);

    public static Finding At(
        string checkId,
        string path,
        SyntaxToken token,
        string message,
        string? replacement = null) =>
        At(checkId, path, token.GetLocation(), message, replacement);

    public static Finding At(string checkId, string path, int line, int column, string message)
    {
        var file = path.Replace('\\', '/');
        return new Finding(
            Id: $"{checkId}:{file}:{line}:{column}",
            Check: checkId,
            File: file,
            Line: line,
            Column: column,
            Message: message);
    }
}

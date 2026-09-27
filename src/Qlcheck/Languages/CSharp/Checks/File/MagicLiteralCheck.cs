using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Checks.File;

public sealed class MagicLiteralCheck : IFileCheck
{
    private const string CheckIdValue = "magic-literal";

    private const string ExceptionSuffix = "Exception";

    private const int TruncateLimit = 40;

    private const int TruncateKeep = 37;

    private const string Ellipsis = "...";

    public static string CheckId => CheckIdValue;

    public string Id => CheckId;

    public string Language => CSharpLanguage.LanguageId;

    public static string NumberMessage(string literal) =>
        $"Magic number {literal} should be a named const or enum member.";

    public static string StringMessage(string literal) =>
        $"Magic string \"{literal}\" should be a named const.";

    public IReadOnlyList<Finding> Analyze(SourceFile file, SyntaxTree tree) =>
        tree.GetRoot().DescendantNodes()
            .OfType<LiteralExpressionSyntax>()
            .Select(literal => TryCreateFinding(file.Path, literal))
            .OfType<Finding>()
            .ToList();

    private static Finding? TryCreateFinding(string path, LiteralExpressionSyntax literal)
    {
        var kind = literal.Kind();
        var message = kind switch
        {
            SyntaxKind.NumericLiteralExpression => TryNumberMessage(literal),
            SyntaxKind.StringLiteralExpression => TryStringMessage(literal),
            _ => null,
        };

        return message is null ? null : Finding.At(CheckId, path, literal, message);
    }

    private static string? TryNumberMessage(LiteralExpressionSyntax literal)
    {
        if (IsAllowedNumber(literal.Token.Value) || IsIgnoredContext(literal))
        {
            return null;
        }

        return NumberMessage(literal.Token.Text);
    }

    private static string? TryStringMessage(LiteralExpressionSyntax literal)
    {
        var value = literal.Token.ValueText;
        if (value.Length == 0 || IsMessage(value, literal) || IsDottedName(value) || SqlText.LooksLikeSql(value))
        {
            return null;
        }

        if (IsIgnoredContext(literal) || IsCollectionElement(literal))
        {
            return null;
        }

        return StringMessage(Truncate(value));
    }

    private static bool IsDottedName(string value)
    {
        var segments = value.Split('.');
        return segments.Length > 1 && segments.All(IsIdentifierSegment);
    }

    private static bool IsIdentifierSegment(string segment) =>
        segment.Length > 0 && IsIdentifierStart(segment[0]) && segment.Skip(1).All(IsIdentifierPart);

    private static bool IsIdentifierStart(char ch) => ch is '_' or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');

    private static bool IsIdentifierPart(char ch) =>
        ch is '_' or (>= '0' and <= '9') or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');

    private static bool IsMessage(string value, LiteralExpressionSyntax literal)
    {
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                return true;
            }
        }

        for (var node = literal.Parent; node is not null; node = node.Parent)
        {
            if (node is ThrowStatementSyntax or ThrowExpressionSyntax)
            {
                return true;
            }

            if (node is ObjectCreationExpressionSyntax creation
                && GetTypeName(creation.Type).EndsWith(ExceptionSuffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetTypeName(TypeSyntax type)
    {
        return type switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            QualifiedNameSyntax q => q.Right.Identifier.Text,
            _ => type.ToString(),
        };
    }

    private static bool IsCollectionElement(LiteralExpressionSyntax literal)
    {
        for (var node = literal.Parent; node is not null; node = node.Parent)
        {
            if (node is CollectionExpressionSyntax)
            {
                return true;
            }

            if (node is InitializerExpressionSyntax init
                && (init.IsKind(SyntaxKind.ArrayInitializerExpression)
                    || init.IsKind(SyntaxKind.CollectionInitializerExpression)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsIgnoredContext(LiteralExpressionSyntax literal)
    {
        for (var node = literal.Parent; node is not null; node = node.Parent)
        {
            if (node is AttributeSyntax)
            {
                return true;
            }
        }

        if (literal.Parent is not EqualsValueClauseSyntax equals)
        {
            return false;
        }

        if (equals.Parent is EnumMemberDeclarationSyntax)
        {
            return true;
        }

        if (equals.Parent is not VariableDeclaratorSyntax)
        {
            return false;
        }

        var declaration = equals.Parent.Parent?.Parent;
        return declaration switch
        {
            LocalDeclarationStatementSyntax local => local.Modifiers.Any(SyntaxKind.ConstKeyword),
            FieldDeclarationSyntax fieldDeclaration => fieldDeclaration.Modifiers.Any(SyntaxKind.ConstKeyword),
            _ => false,
        };
    }

    private static bool IsAllowedNumber(object? value) =>
        value switch
        {
            decimal m => m is 0 or 1,
            IConvertible number => number.ToDouble(CultureInfo.InvariantCulture) is 0 or 1,
            _ => false,
        };

    private static string Truncate(string value) =>
        value.Length <= TruncateLimit ? value : $"{value[..TruncateKeep]}{Ellipsis}";
}

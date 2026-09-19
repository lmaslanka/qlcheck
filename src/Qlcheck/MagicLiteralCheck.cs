using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck;

public sealed class MagicLiteralCheck : ICheck
{
    public const string CheckId = "magic-literal";

    public string Id => CheckId;

    public static string NumberMessage(string literal) =>
        $"Magic number {literal} should be a named const or enum member.";

    public static string StringMessage(string literal) =>
        $"Magic string \"{literal}\" should be a named const.";

    public IReadOnlyList<Finding> Analyze(SourceFile file, SyntaxTree tree)
    {
        var findings = new List<Finding>();
        foreach (var literal in tree.GetRoot().DescendantNodes().OfType<LiteralExpressionSyntax>())
        {
            if (TryCreateFinding(file.Path, literal, out var finding))
            {
                findings.Add(finding);
            }
        }

        return findings;
    }

    private static bool TryCreateFinding(string path, LiteralExpressionSyntax literal, out Finding finding)
    {
        finding = null!;
        string message;
        var kind = literal.Kind();
        if (kind == SyntaxKind.NumericLiteralExpression)
        {
            if (IsAllowedNumber(literal.Token.Value))
            {
                return false;
            }

            if (IsIgnoredContext(literal))
            {
                return false;
            }

            message = NumberMessage(literal.Token.Text);
        }
        else if (kind == SyntaxKind.StringLiteralExpression)
        {
            var value = literal.Token.ValueText;
            if (value.Length == 0 || IsMessage(value, literal) || IsDottedName(value) || SqlText.LooksLikeSql(value))
            {
                return false;
            }

            if (IsIgnoredContext(literal))
            {
                return false;
            }

            message = StringMessage(Truncate(value));
        }
        else
        {
            return false;
        }

        var span = literal.GetLocation().GetLineSpan().StartLinePosition;
        var line = span.Line + 1;
        var column = span.Character + 1;
        var file = path.Replace('\\', '/');
        finding = new Finding(
            Id: $"{CheckId}:{file}:{line}:{column}",
            Check: CheckId,
            File: file,
            Line: line,
            Column: column,
            Message: message);
        return true;
    }

    private static bool IsDottedName(string value)
    {
        var dot = false;
        var start = true;
        foreach (var ch in value)
        {
            if (ch == '.')
            {
                if (start)
                {
                    return false;
                }

                dot = true;
                start = true;
                continue;
            }

            if (start)
            {
                if (ch is not ('_' or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z')))
                {
                    return false;
                }

                start = false;
                continue;
            }

            if (ch is not ('_' or (>= '0' and <= '9') or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z')))
            {
                return false;
            }
        }

        return dot && !start;
    }

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

            if (node is ObjectCreationExpressionSyntax creation &&
                GetTypeName(creation.Type).EndsWith("Exception", StringComparison.Ordinal))
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
            FieldDeclarationSyntax field => field.Modifiers.Any(SyntaxKind.ConstKeyword),
            _ => false,
        };
    }

    private static bool IsAllowedNumber(object? value) =>
        value switch
        {
            int i => i is 0 or 1,
            long l => l is 0 or 1,
            uint u => u is 0 or 1,
            ulong ul => ul is 0 or 1,
            byte b => b is 0 or 1,
            sbyte sb => sb is 0 or 1,
            short s => s is 0 or 1,
            ushort us => us is 0 or 1,
            float f => f is 0 or 1,
            double d => d is 0 or 1,
            decimal m => m is 0 or 1,
            _ => false,
        };

    private static string Truncate(string value) =>
        value.Length <= 40 ? value : value[..37] + "...";
}

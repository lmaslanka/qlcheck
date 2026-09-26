using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Checks.File;

public sealed class StringConcatCheck : IFileCheck
{
    public const string CheckId = "string-concat";

    private const string ConcatMethod = "Concat";

    private const int MinConcatArguments = 2;

    private const string StringKeyword = "string";

    private const string StringTypeName = "String";

    private const string EscapedBackslash = "\\\\";

    private const string EscapedQuote = "\\\"";

    private const string EscapedOpenBrace = "{{";

    private const string EscapedCloseBrace = "}}";

    private const string EscapedNewline = "\\n";

    private const string EscapedReturn = "\\r";

    private const string EscapedTab = "\\t";

    private const string InterpolationPrefix = "$\"";

    private const string InterpolationSuffix = "\"";

    public const string Message = "String concatenation should be string interpolation.";

    public string Id => CheckId;

    public string Language => CSharpLanguage.LanguageId;

    public IReadOnlyList<Finding> Analyze(SourceFile file, SyntaxTree tree)
    {
        var findings = new List<Finding>();
        foreach (var node in tree.GetRoot().DescendantNodes())
        {
            switch (node)
            {
                case BinaryExpressionSyntax binary
                    when binary.IsKind(SyntaxKind.AddExpression) &&
                         IsStringConcat(binary) &&
                         IsOutermost(binary) &&
                         !IsIgnoredContext(binary) &&
                         !IsSqlConcat(binary):
                    findings.Add(Finding.At(CheckId, file.Path, binary, Message, TryInterpolation(binary)));
                    break;
                case InvocationExpressionSyntax invocation
                    when IsStringConcatCall(invocation) &&
                         !IsIgnoredContext(invocation) &&
                         !IsSqlConcatCall(invocation):
                    findings.Add(Finding.At(CheckId, file.Path, invocation, Message));
                    break;
            }
        }

        return findings;
    }

    private static bool IsSqlConcat(BinaryExpressionSyntax binary)
    {
        var parts = new List<ExpressionSyntax>();
        Flatten(binary, parts);
        foreach (var part in parts)
        {
            if (LooksLikeSql(Unwrap(part)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSqlConcatCall(InvocationExpressionSyntax invocation)
    {
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (LooksLikeSql(Unwrap(arg.Expression)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeSql(ExpressionSyntax expression)
    {
        if (expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return SqlText.LooksLikeSql(literal.Token.ValueText);
        }

        if (expression is InterpolatedStringExpressionSyntax interpolated)
        {
            return SqlText.LooksLikeSql(interpolated.ToFullString());
        }

        return false;
    }

    private static bool IsStringConcat(BinaryExpressionSyntax binary) =>
        IsStringy(binary.Left) || IsStringy(binary.Right);

    private static bool IsStringy(ExpressionSyntax expression)
    {
        expression = Unwrap(expression);
        if (expression.IsKind(SyntaxKind.StringLiteralExpression) ||
            expression is InterpolatedStringExpressionSyntax)
        {
            return true;
        }

        return expression is BinaryExpressionSyntax binary &&
               binary.IsKind(SyntaxKind.AddExpression) &&
               IsStringConcat(binary);
    }

    private static bool IsOutermost(BinaryExpressionSyntax binary)
    {
        var parent = binary.Parent;
        while (parent is ParenthesizedExpressionSyntax paren)
        {
            parent = paren.Parent;
        }

        return parent is not BinaryExpressionSyntax parentAdd ||
               !parentAdd.IsKind(SyntaxKind.AddExpression) ||
               !IsStringConcat(parentAdd);
    }

    private static bool IsStringConcatCall(InvocationExpressionSyntax invocation)
    {
        if (GetInvokedName(invocation) != ConcatMethod || invocation.ArgumentList.Arguments.Count < MinConcatArguments)
        {
            return false;
        }

        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => IsStringTypeName(member.Expression),
            _ => false,
        };
    }

    private static bool IsStringTypeName(ExpressionSyntax expression)
    {
        expression = Unwrap(expression);
        return expression switch
        {
            IdentifierNameSyntax id => id.Identifier.Text is StringKeyword or StringTypeName,
            PredefinedTypeSyntax pre => pre.Keyword.IsKind(SyntaxKind.StringKeyword),
            MemberAccessExpressionSyntax member => member.Name.Identifier.Text is StringTypeName,
            QualifiedNameSyntax q => q.Right.Identifier.Text is StringTypeName,
            _ => false,
        };
    }

    private static string GetInvokedName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
            IdentifierNameSyntax id => id.Identifier.Text,
            _ => string.Empty,
        };
    }

    private static bool IsIgnoredContext(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is AttributeSyntax)
            {
                return true;
            }

            if (current is LocalDeclarationStatementSyntax local)
            {
                return local.Modifiers.Any(SyntaxKind.ConstKeyword);
            }

            if (current is FieldDeclarationSyntax field)
            {
                return field.Modifiers.Any(SyntaxKind.ConstKeyword);
            }
        }

        return false;
    }

    private static string? TryInterpolation(BinaryExpressionSyntax binary)
    {
        var parts = new List<ExpressionSyntax>();
        Flatten(binary, parts);
        var inner = new StringBuilder();
        foreach (var part in parts)
        {
            var expr = Unwrap(part);
            if (expr is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                foreach (var ch in literal.Token.ValueText)
                {
                    inner.Append(ch switch
                    {
                        '\\' => EscapedBackslash,
                        '"' => EscapedQuote,
                        '{' => EscapedOpenBrace,
                        '}' => EscapedCloseBrace,
                        '\n' => EscapedNewline,
                        '\r' => EscapedReturn,
                        '\t' => EscapedTab,
                        _ => ch.ToString(),
                    });
                }
            }
            else if (expr is InterpolatedStringExpressionSyntax)
            {
                return null;
            }
            else
            {
                inner.Append('{');
                inner.Append(expr.WithoutTrivia().ToFullString());
                inner.Append('}');
            }
        }

        return $"{InterpolationPrefix}{inner}{InterpolationSuffix}";
    }

    private static void Flatten(ExpressionSyntax expression, List<ExpressionSyntax> parts)
    {
        expression = Unwrap(expression);
        if (expression is BinaryExpressionSyntax binary &&
            binary.IsKind(SyntaxKind.AddExpression) &&
            IsStringConcat(binary))
        {
            Flatten(binary.Left, parts);
            Flatten(binary.Right, parts);
            return;
        }

        parts.Add(expression);
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax paren)
        {
            expression = paren.Expression;
        }

        return expression;
    }
}

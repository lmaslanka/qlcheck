using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck;

public sealed class StringEmptyCheck : ICheck
{
    public const string CheckId = "string-empty";

    public const string Message = "Use string.Empty instead of \"\".";

    public string Id => CheckId;

    public IReadOnlyList<Finding> Analyze(SourceFile file, SyntaxTree tree)
    {
        var findings = new List<Finding>();
        foreach (var literal in tree.GetRoot().DescendantNodes().OfType<LiteralExpressionSyntax>())
        {
            if (!literal.IsKind(SyntaxKind.StringLiteralExpression) ||
                literal.Token.ValueText.Length != 0 ||
                IsConstantContext(literal))
            {
                continue;
            }

            var span = literal.GetLocation().GetLineSpan().StartLinePosition;
            var line = span.Line + 1;
            var column = span.Character + 1;
            var path = file.Path.Replace('\\', '/');
            findings.Add(new Finding(
                Id: $"{CheckId}:{path}:{line}:{column}",
                Check: CheckId,
                File: path,
                Line: line,
                Column: column,
                Message: Message,
                Replacement: "string.Empty"));
        }

        return findings;
    }

    private static bool IsConstantContext(LiteralExpressionSyntax literal)
    {
        for (var node = literal.Parent; node is not null; node = node.Parent)
        {
            switch (node)
            {
                case AttributeSyntax:
                case ParameterSyntax:
                case CaseSwitchLabelSyntax:
                case ConstantPatternSyntax:
                    return true;
                case LocalDeclarationStatementSyntax local:
                    return local.Modifiers.Any(SyntaxKind.ConstKeyword);
                case FieldDeclarationSyntax field:
                    return field.Modifiers.Any(SyntaxKind.ConstKeyword);
            }
        }

        return false;
    }
}

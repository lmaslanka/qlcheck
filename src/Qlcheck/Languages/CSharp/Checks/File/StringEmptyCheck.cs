using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Checks.File;

public sealed class StringEmptyCheck : IFileCheck
{
    public const string CheckId = "string-empty";

    public const string Message = "Use string.Empty instead of \"\".";

    public string Id => CheckId;

    public string Language => CSharpLanguage.LanguageId;

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

            findings.Add(Finding.At(CheckId, file.Path, literal, Message, "string.Empty"));
        }

        return findings;
    }

    private static bool IsConstantContext(LiteralExpressionSyntax literal)
    {
        for (var node = literal.Parent; node is not null; node = node.Parent)
        {
            var constant = node switch
            {
                AttributeSyntax or ParameterSyntax or CaseSwitchLabelSyntax or ConstantPatternSyntax => true,
                LocalDeclarationStatementSyntax local => local.Modifiers.Any(SyntaxKind.ConstKeyword),
                FieldDeclarationSyntax field => field.Modifiers.Any(SyntaxKind.ConstKeyword),
                _ => (bool?)null,
            };
            if (constant is bool result)
            {
                return result;
            }
        }

        return false;
    }
}

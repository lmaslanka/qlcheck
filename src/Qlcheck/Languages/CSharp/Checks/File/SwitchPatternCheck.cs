using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Checks.File;

public sealed class SwitchPatternCheck : IFileCheck
{
    public const string CheckId = "switch-pattern";

    private const int MinSections = 2;

    public const string Message = "This switch statement should be a pattern-matching switch expression.";

    public string Id => CheckId;

    public string Language => CSharpLanguage.LanguageId;

    public IReadOnlyList<Finding> Analyze(SourceFile file, SyntaxTree tree)
    {
        var findings = new List<Finding>();
        foreach (var statement in tree.GetRoot().DescendantNodes().OfType<SwitchStatementSyntax>())
        {
            if (!IsCandidate(statement))
            {
                continue;
            }

            findings.Add(Finding.At(CheckId, file.Path, statement.SwitchKeyword, Message));
        }

        return findings;
    }

    private static bool IsCandidate(SwitchStatementSyntax statement)
    {
        if (statement.Sections.Count < MinSections)
        {
            return false;
        }

        ArmKind? required = null;
        string? assignTarget = null;
        foreach (var section in statement.Sections)
        {
            if (section.DescendantNodes().OfType<GotoStatementSyntax>().Any())
            {
                return false;
            }

            if (!TryArm(section, out var kind, out var target))
            {
                return false;
            }

            if (kind == ArmKind.Throw)
            {
                continue;
            }

            if (required is null)
            {
                required = kind;
                assignTarget = target;
                continue;
            }

            if (kind != required)
            {
                return false;
            }

            if (kind == ArmKind.Assign && target != assignTarget)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryArm(SwitchSectionSyntax section, out ArmKind kind, out string? assignTarget)
    {
        kind = default;
        assignTarget = null;
        var statements = Unwrap(section.Statements);
        while (statements.Count > 0 && statements[^1] is BreakStatementSyntax)
        {
            statements.RemoveAt(statements.Count - 1);
        }

        if (statements.Count != 1)
        {
            return false;
        }

        switch (statements[0])
        {
            case ReturnStatementSyntax { Expression: not null }:
                kind = ArmKind.Return;
                return true;
            case ThrowStatementSyntax { Expression: not null }:
                kind = ArmKind.Throw;
                return true;
            case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
                when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression):
                kind = ArmKind.Assign;
                assignTarget = assignment.Left.WithoutTrivia().ToFullString();
                return true;
            default:
                return false;
        }
    }

    private static List<StatementSyntax> Unwrap(SyntaxList<StatementSyntax> statements)
    {
        if (statements.Count == 1 && statements[0] is BlockSyntax { Statements.Count: > 0 } block)
        {
            return [.. block.Statements];
        }

        return [.. statements];
    }

    private enum ArmKind
    {
        Return,
        Assign,
        Throw,
    }
}

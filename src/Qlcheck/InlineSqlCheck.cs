using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck;

public sealed class InlineSqlCheck : IFileCheck
{
    public const string LayoutMessage =
        "Inline SQL must be a C# raw string literal with qlfmt layout.";

    public const string UnformattableMessage = "Inline SQL is not a single string literal.";

    public const string FormatFailedMessage = "Inline SQL could not be formatted.";

    public const string CheckId = "inline-sql";

    private static readonly HashSet<string> DapperMethods = new(StringComparer.Ordinal)
    {
        "Query", "QueryAsync",
        "QueryFirst", "QueryFirstAsync",
        "QuerySingle", "QuerySingleAsync",
        "QueryFirstOrDefault", "QueryFirstOrDefaultAsync",
        "QuerySingleOrDefault", "QuerySingleOrDefaultAsync",
        "Execute", "ExecuteAsync",
        "ExecuteScalar", "ExecuteScalarAsync",
        "QueryMultiple", "QueryMultipleAsync",
        "ExecuteReader", "ExecuteReaderAsync",
    };

    private static readonly HashSet<string> CommandTypes = new(StringComparer.Ordinal)
    {
        "SqlCommand", "NpgsqlCommand", "SqliteCommand", "MySqlCommand", "DbCommand",
    };

    public string Id => CheckId;

    public bool EnabledByDefault => false;

    public IReadOnlyList<Finding> Analyze(SourceFile file, SyntaxTree tree)
    {
        var findings = new List<Finding>();
        var finder = new SqlExpressionFinder();
        finder.Visit(tree.GetRoot());
        foreach (var expression in finder.Expressions)
        {
            if (TryCreateFinding(file.Path, tree, expression, out var finding))
            {
                findings.Add(finding);
            }
        }

        return findings;
    }

    private sealed class SqlExpressionFinder : CSharpSyntaxWalker
    {
        public List<ExpressionSyntax> Expressions { get; } = [];

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (DapperMethods.Contains(GetInvokedName(node)))
            {
                var sql = GetSqlArgument(node);
                if (sql is not null)
                {
                    Expressions.Add(sql);
                }
            }

            base.VisitInvocationExpression(node);
        }

        public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            if (node.Left is MemberAccessExpressionSyntax member &&
                member.Name.Identifier.Text == "CommandText")
            {
                Expressions.Add(node.Right);
            }

            base.VisitAssignmentExpression(node);
        }

        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            if (CommandTypes.Contains(GetTypeName(node.Type)))
            {
                var args = node.ArgumentList?.Arguments;
                if (args is { Count: > 0 })
                {
                    var named = args.Value.FirstOrDefault(a =>
                        a.NameColon?.Name.Identifier.Text is "cmdText" or "commandText" or "cmd");
                    Expressions.Add((named ?? args.Value[0]).Expression);
                }
            }

            base.VisitObjectCreationExpression(node);
        }
    }

    private static string GetInvokedName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.Text,
            IdentifierNameSyntax id => id.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            _ => "",
        };
    }

    private static ExpressionSyntax? GetSqlArgument(InvocationExpressionSyntax invocation)
    {
        var args = invocation.ArgumentList.Arguments;
        if (args.Count == 0)
        {
            return null;
        }

        var named = args.FirstOrDefault(a => a.NameColon?.Name.Identifier.Text == "sql");
        return (named ?? args[0]).Expression;
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

    private static bool TryCreateFinding(
        string path,
        SyntaxTree tree,
        ExpressionSyntax expression,
        out Finding finding)
    {
        finding = null!;
        var resolved = Resolve(expression);
        if (resolved is UnresolvedSql)
        {
            return false;
        }

        if (resolved is UnformattableSql unformattable)
        {
            finding = Finding.At(CheckId, path, unformattable.Node, UnformattableMessage);
            return true;
        }

        if (resolved is not LiteralSql literal)
        {
            return false;
        }

        string formatted;
        try
        {
            formatted = QlFmt.Sql.Format(literal.Value);
        }
        catch (QlParse.SqlParseException ex)
        {
            finding = Finding.At(
                CheckId,
                path,
                literal.Node,
                $"{FormatFailedMessage} {ex.Message} at {ex.Position}");
            return true;
        }

        if (IsRawString(literal.Node) && ValuesEqual(literal.Value, formatted))
        {
            return false;
        }

        var line = tree.GetText().Lines[literal.Node.GetLocation().GetLineSpan().StartLinePosition.Line];
        var contentIndent = line.ToString().TakeWhile(char.IsWhiteSpace).Count() + 4;
        var replacement = ToRawStringLiteral(formatted, contentIndent);
        finding = Finding.At(CheckId, path, literal.Node, LayoutMessage, replacement);
        return true;
    }

    private static SqlTarget Resolve(ExpressionSyntax expression)
    {
        expression = Unwrap(expression);
        if (IsStringLiteral(expression, out var value))
        {
            return new LiteralSql(expression, value);
        }

        if (expression is InterpolatedStringExpressionSyntax interpolated)
        {
            return new UnformattableSql(interpolated);
        }

        if (expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression))
        {
            return new UnformattableSql(binary);
        }

        if (expression is IdentifierNameSyntax id)
        {
            return ResolveIdentifier(id);
        }

        return new UnresolvedSql();
    }

    private static SqlTarget ResolveIdentifier(IdentifierNameSyntax id)
    {
        var name = id.Identifier.Text;
        var use = id.SpanStart;
        ExpressionSyntax? lastWrite = null;
        var lastPos = -1;

        foreach (var declarator in id.Ancestors().SelectMany(a =>
                     a.ChildNodes().OfType<LocalDeclarationStatementSyntax>()
                         .SelectMany(d => d.Declaration.Variables)))
        {
            if (declarator.Identifier.Text != name || declarator.SpanStart >= use)
            {
                continue;
            }

            var block = declarator.Ancestors().OfType<BlockSyntax>().FirstOrDefault();
            if (block is null || !id.Ancestors().Contains(block))
            {
                continue;
            }

            if (declarator.SpanStart >= lastPos && declarator.Initializer is not null)
            {
                lastPos = declarator.SpanStart;
                lastWrite = declarator.Initializer.Value;
            }
        }

        var member = id.Ancestors().FirstOrDefault(a =>
            a is MethodDeclarationSyntax
                or ConstructorDeclarationSyntax
                or LocalFunctionStatementSyntax
                or AnonymousFunctionExpressionSyntax
                or AccessorDeclarationSyntax);

        if (member is not null)
        {
            foreach (var assignment in member.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (assignment.SpanStart >= use)
                {
                    continue;
                }

                if (assignment.Left is IdentifierNameSyntax left &&
                    left.Identifier.Text == name &&
                    assignment.SpanStart >= lastPos)
                {
                    lastPos = assignment.SpanStart;
                    lastWrite = assignment.Right;
                }
            }
        }

        if (lastWrite is null)
        {
            return new UnresolvedSql();
        }

        lastWrite = Unwrap(lastWrite);
        if (IsStringLiteral(lastWrite, out var value))
        {
            return new LiteralSql(lastWrite, value);
        }

        if (lastWrite is InterpolatedStringExpressionSyntax or BinaryExpressionSyntax)
        {
            return new UnformattableSql(lastWrite);
        }

        return new UnresolvedSql();
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax paren)
        {
            expression = paren.Expression;
        }

        return expression;
    }

    private static bool IsStringLiteral(ExpressionSyntax expression, out string value)
    {
        if (expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            value = literal.Token.ValueText;
            return true;
        }

        value = "";
        return false;
    }

    private static bool IsRawString(ExpressionSyntax expression)
    {
        return expression is LiteralExpressionSyntax literal &&
               (literal.Token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken) ||
                literal.Token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken));
    }

    private static bool ValuesEqual(string left, string right) =>
        left.ReplaceLineEndings("\n").TrimEnd() == right.ReplaceLineEndings("\n").TrimEnd();

    private static string ToRawStringLiteral(string sql, int contentIndent)
    {
        var indent = new string(' ', contentIndent);
        var lines = sql.ReplaceLineEndings("\n").Split('\n');
        var sb = new StringBuilder();
        sb.Append("\"\"\"\n");
        foreach (var line in lines)
        {
            sb.Append(indent).Append(line).Append('\n');
        }

        sb.Append(indent).Append("\"\"\"");
        return sb.ToString();
    }

    private abstract record SqlTarget;

    private sealed record LiteralSql(ExpressionSyntax Node, string Value) : SqlTarget;

    private sealed record UnformattableSql(ExpressionSyntax Node) : SqlTarget;

    private sealed record UnresolvedSql : SqlTarget;
}

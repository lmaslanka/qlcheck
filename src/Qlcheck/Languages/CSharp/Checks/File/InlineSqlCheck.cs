using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Checks.File;

public sealed class InlineSqlCheck : IFileCheck
{
    private const string LayoutMessageValue =
        "Inline SQL must be a C# raw string literal with qlfmt layout.";

    private const string UnformattableMessageValue = "Inline SQL is not a single string literal.";

    private const string FormatFailedMessageValue = "Inline SQL could not be formatted.";

    private const string CheckIdValue = "inline-sql";

    private const string CommandTextProperty = "CommandText";

    private const string CmdTextArgument = "cmdText";

    private const string CommandTextArgument = "commandText";

    private const string CmdArgument = "cmd";

    private const string SqlArgument = "sql";

    private const int RawStringExtraIndent = 4;

    private const char QuoteChar = '"';

    private const int TripleQuoteCount = 3;

    private static readonly Func<string, bool, string> FormatSql = QlFmt.Sql.Format;

    private static readonly string NewlineText = '\n'.ToString();

    private static readonly string RawStringDelimiter = new(QuoteChar, TripleQuoteCount);

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

    public static string LayoutMessage => LayoutMessageValue;

    public static string UnformattableMessage => UnformattableMessageValue;

    public static string FormatFailedMessage => FormatFailedMessageValue;

    public static string CheckId => CheckIdValue;

    public string Id => CheckId;

    public string Language => CSharpLanguage.LanguageId;

    public bool EnabledByDefault => false;

    public IReadOnlyList<Finding> Analyze(SourceFile file, SyntaxTree tree)
    {
        var finder = new SqlExpressionFinder();
        finder.Visit(tree.GetRoot());
        return finder.Expressions
            .Select(expression => TryCreateFinding(file.Path, tree, expression))
            .OfType<Finding>()
            .ToList();
    }

    private static Finding? TryCreateFinding(string path, SyntaxTree tree, ExpressionSyntax expression)
    {
        var resolved = Resolve(expression);
        if (resolved is UnresolvedSql)
        {
            return null;
        }

        if (resolved is UnformattableSql unformattable)
        {
            return Finding.At(CheckId, path, unformattable.Node, UnformattableMessage);
        }

        if (resolved is not LiteralSql literal)
        {
            return null;
        }

        string formatted;
        try
        {
            formatted = FormatSql(literal.Value, false);
        }
        catch (QlParse.SqlParseException ex)
        {
            return Finding.At(
                CheckId,
                path,
                literal.Node,
                $"{FormatFailedMessage} {ex.Message} at {ex.Position}");
        }

        if (IsRawString(literal.Node) && ValuesEqual(literal.Value, formatted))
        {
            return null;
        }

        var line = tree.GetText().Lines[literal.Node.GetLocation().GetLineSpan().StartLinePosition.Line];
        var contentIndent = line.ToString().TakeWhile(char.IsWhiteSpace).Count() + RawStringExtraIndent;
        var replacement = ToRawStringLiteral(formatted, contentIndent);
        return Finding.At(CheckId, path, literal.Node, LayoutMessage, replacement);
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
        var tracker = new LastWriteTracker();
        TrackLocalDeclarations(id, name, use, tracker);
        TrackAssignments(id, name, use, tracker);

        if (tracker.Expression is not { } lastWrite)
        {
            return new UnresolvedSql();
        }

        return Classify(Unwrap(lastWrite));
    }

    private static void TrackLocalDeclarations(IdentifierNameSyntax id, string name, int use, LastWriteTracker tracker)
    {
        foreach (var declarator in id.Ancestors().SelectMany(a =>
                     a.ChildNodes().OfType<LocalDeclarationStatementSyntax>()
                         .SelectMany(d => d.Declaration.Variables)))
        {
            if (declarator.Identifier.Text != name || declarator.SpanStart >= use || declarator.Initializer is null)
            {
                continue;
            }

            var block = declarator.Ancestors().OfType<BlockSyntax>().FirstOrDefault();
            if (block is null || !id.Ancestors().Contains(block))
            {
                continue;
            }

            tracker.Consider(declarator.SpanStart, declarator.Initializer.Value);
        }
    }

    private static void TrackAssignments(IdentifierNameSyntax id, string name, int use, LastWriteTracker tracker)
    {
        var member = id.Ancestors().FirstOrDefault(a =>
            a is MethodDeclarationSyntax
                or ConstructorDeclarationSyntax
                or LocalFunctionStatementSyntax
                or AnonymousFunctionExpressionSyntax
                or AccessorDeclarationSyntax);

        if (member is null)
        {
            return;
        }

        foreach (var assignment in member.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.SpanStart >= use
                || assignment.Left is not IdentifierNameSyntax left
                || left.Identifier.Text != name)
            {
                continue;
            }

            tracker.Consider(assignment.SpanStart, assignment.Right);
        }
    }

    private static SqlTarget Classify(ExpressionSyntax expression)
    {
        if (IsStringLiteral(expression, out var value))
        {
            return new LiteralSql(expression, value);
        }

        return expression is InterpolatedStringExpressionSyntax or BinaryExpressionSyntax
            ? new UnformattableSql(expression)
            : new UnresolvedSql();
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
        if (expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            value = literal.Token.ValueText;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool IsRawString(ExpressionSyntax expression)
    {
        return expression is LiteralExpressionSyntax literal
               && (literal.Token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken)
                   || literal.Token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken));
    }

    private static bool ValuesEqual(string left, string right) =>
        left.ReplaceLineEndings(NewlineText).TrimEnd() == right.ReplaceLineEndings(NewlineText).TrimEnd();

    private static string ToRawStringLiteral(string sql, int contentIndent)
    {
        var indent = new string(' ', contentIndent);
        var lines = sql.ReplaceLineEndings(NewlineText).Split('\n');
        var body = string.Concat(lines.Select(line => $"{indent}{line}{NewlineText}"));
        return $"{RawStringDelimiter}{NewlineText}{body}{indent}{RawStringDelimiter}";
    }

    private sealed class LastWriteTracker
    {
        public int Position { get; private set; } = -1;

        public ExpressionSyntax? Expression { get; private set; }

        public void Consider(int position, ExpressionSyntax expression)
        {
            if (position < Position)
            {
                return;
            }

            Position = position;
            Expression = expression;
        }
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
            if (node.Left is MemberAccessExpressionSyntax member
                && member.Name.Identifier.Text == CommandTextProperty)
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
                        a.NameColon?.Name.Identifier.Text is CmdTextArgument or CommandTextArgument or CmdArgument);
                    Expressions.Add((named ?? args.Value[0]).Expression);
                }
            }

            base.VisitObjectCreationExpression(node);
        }

        private static string GetInvokedName(InvocationExpressionSyntax invocation)
        {
            return invocation.Expression switch
            {
                MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
                MemberBindingExpressionSyntax binding => binding.Name.Identifier.Text,
                IdentifierNameSyntax id => id.Identifier.Text,
                GenericNameSyntax generic => generic.Identifier.Text,
                _ => string.Empty,
            };
        }

        private static ExpressionSyntax? GetSqlArgument(InvocationExpressionSyntax invocation)
        {
            var args = invocation.ArgumentList.Arguments;
            if (args.Count == 0)
            {
                return null;
            }

            var named = args.FirstOrDefault(a => a.NameColon?.Name.Identifier.Text == SqlArgument);
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
    }

    private abstract record SqlTarget;

    private sealed record LiteralSql(ExpressionSyntax Node, string Value) : SqlTarget;

    private sealed record UnformattableSql(ExpressionSyntax Node) : SqlTarget;

    private sealed record UnresolvedSql : SqlTarget;
}

// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class Patterns
{
    private const string ExceptionName = "Exception";

    private const string NullReferenceName = "NullReferenceException";

    private const string NotImplementedName = "NotImplementedException";

    private const string ControllerName = "Controller";

    private const string FlagsSuffix = "Flags";

    private const string EnumSuffix = "Enum";

    private const string AsyncSuffixName = "Async";

    private const string FactName = "Fact";

    private const string TestName = "Test";

    private const string TestsSuffix = "Tests";

    private const string TestSuffix = "Test";

    private const string TestMethodName = "TestMethod";

    private const string IgnoreName = "Ignore";

    private const string SkipName = "Skip";

    private const string AssertName = "Assert";

    private const string SleepName = "Sleep";

    private const string VoidName = "void";

    private const string LongName = "long";

    public static void EmptyMethod(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.MethodDeclaration, Shapes.IsEmptyMethod);

    public static void EmptyClass(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ClassDeclaration, IsEmptyType);

    public static void EmptyInterface(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.InterfaceDeclaration, IsEmptyType);

    public static void EmptyNamespace(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.NamespaceDeclaration, IsEmptyType);

    public static void EmptyFinalizer(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.DestructorDeclaration, IsEmptyBody);

    public static void EmptyBlock(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.Block))
        {
            if (Shapes.IsEmptyBlock(node) && node.Parent is BlockSyntax)
            {
                ctx.Report(id, node);
            }
        }
    }

    public static void Braces(WalkContext ctx, string id) =>
        ReportAny(ctx, id, StmtFacts.NeedsBraces);

    public static void Parens(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ParenthesizedExpression, StmtFacts.RedundantParen);

    public static void Newline(WalkContext ctx, string id)
    {
        if (FileFacts.MissingNewline(ctx.Text))
        {
            ctx.Report(id, ctx.Tree.GetRoot());
        }
    }

    public static void Tabs(WalkContext ctx, string id)
    {
        if (FileFacts.HasTab(ctx.Text))
        {
            ctx.Report(id, ctx.Tree.GetRoot());
        }
    }

    public static void Copyright(WalkContext ctx, string id)
    {
        if (FileFacts.MissingCopyright(ctx.Tree))
        {
            ctx.Report(id, ctx.Tree.GetRoot());
        }
    }

    public static void Commented(WalkContext ctx, string id) => ReportComments(ctx, id, FileFacts.IsCodeComment);

    public static void EmptyComment(WalkContext ctx, string id) => ReportComments(ctx, id, FileFacts.IsEmptyComment);

    public static void PascalType(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ClassDeclaration, NameNotPascal);

    public static void PascalMember(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.MethodDeclaration, NameNotPascal);

    public static void NamedNamespace(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.ClassDeclaration))
        {
            if (!TypeFacts.InNamespace(node))
            {
                ctx.Report(id, node);
            }
        }
    }

    public static void AsyncVoid(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.MethodDeclaration, TypeFacts.IsAsyncVoid);

    public static void PublicField(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.FieldDeclaration, TypeFacts.IsPublicField);

    public static void FieldPrivate(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.FieldDeclaration, TypeFacts.FieldNotPrivate);

    public static void CatchException(WalkContext ctx, string id) => CatchNamed(ctx, id, ExceptionName);

    public static void CatchNull(WalkContext ctx, string id) => CatchNamed(ctx, id, NullReferenceName);

    public static void ThrowException(WalkContext ctx, string id) => ThrowNamed(ctx, id, ExceptionName);

    public static void ThrowNotImplemented(WalkContext ctx, string id) =>
        ThrowNamed(ctx, id, NotImplementedName);

    public static void BareRethrow(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ThrowStatement, CatchFacts.BareRethrow);

    public static void CatchOnlyRethrow(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.CatchClause, CatchFacts.Rethrows);

    public static void NestedSwitch(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.SwitchStatement, StmtFacts.NestedSwitch);

    public static void NestedTernary(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ConditionalExpression, StmtFacts.NestedTernary);

    public static void SwitchDefault(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.SwitchStatement, StmtFacts.MissingSwitchDefault);

    public static void SwitchFew(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.SwitchStatement, StmtFacts.FewSwitchCases);

    public static void SelfAssign(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.SimpleAssignmentExpression, StmtFacts.SelfAssignment);

    public static void IdenticalOps(WalkContext ctx, string id) =>
        ReportAny(ctx, id, StmtFacts.IdenticalOperands);

    public static void LockLocal(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.LockStatement, StmtFacts.LockOnLocal);

    public static void Infinite(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.WhileStatement, StmtFacts.InfiniteWhile);

    public static void Unreachable(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.IfStatement, StmtFacts.UnreachableIf);

    public static void DisposeCreated(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.ObjectCreationExpression))
        {
            if (IsCreatedDisposable(ctx, node) && !IsUsing(node))
            {
                ctx.Report(id, node);
            }
        }
    }

    public static void UnusedLocal(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.LocalDeclarationStatement))
        {
            foreach (var declarator in LocalFacts.Declarators(node))
            {
                if (LocalFacts.Unused(declarator))
                {
                    ctx.Report(id, declarator);
                }
            }
        }
    }

    public static void UnusedPrivate(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.MethodDeclaration, LocalFacts.PrivateUnused);

    public static void NullDeref(WalkContext ctx, string id) => Report(ctx, id, SyntaxKind.IfStatement, IsNullDeref);

    public static void LoopBound(WalkContext ctx, string id) => Report(ctx, id, SyntaxKind.ForStatement, ForUsesParameter);

    public static void AllocDos(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ArrayCreationExpression, ArrayUsesParameter);

    public static void EnumStorage(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.EnumDeclaration))
        {
            if (TypeFacts.BaseType(node) == LongName)
            {
                ctx.Report(id, node);
            }
        }
    }

    public static void LiteralSuffix(WalkContext ctx, string id)
    {
        foreach (var token in ctx.Tree.GetRoot().DescendantTokens())
        {
            if (token.IsKind(SyntaxKind.NumericLiteralToken) && HasLowerSuffix(token.Text))
            {
                ctx.Report(id, token);
            }
        }
    }

    public static void ConstantReturn(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.MethodDeclaration, ReturnsSameLiteral);

    public static void ElseRequired(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.IfStatement, MissingElse);

    public static void IdenticalBranch(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.IfStatement, SameBranch);

    public static void OneStatement(WalkContext ctx, string id) => ReportLines(ctx, id, HasTwoStatements);

    public static void AbstractCtor(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ConstructorDeclaration, PublicCtorOnAbstract);

    public static void SealedProtected(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.MethodDeclaration, ProtectedOnSealed);

    public static void FinalizerThrow(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.DestructorDeclaration, BodyThrows);

    public static void TestAssert(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.MethodDeclaration, MissingAssert);

    public static void TestIgnored(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.Attribute, IsIgnore);

    public static void TestCase(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ClassDeclaration, MissingTest);

    public static void TestSignature(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.MethodDeclaration, BadTestSignature);

    public static void TestSleep(WalkContext ctx, string id) =>
        InvokeMatch.Report(ctx, id, SleepName, null);

    public static void PascalEnum(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.EnumDeclaration, NameNotPascal);

    public static void ExplicitWhitespace(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.StringLiteralExpression))
        {
            if (node is LiteralExpressionSyntax literal && literal.Token.Text.Contains('\\'))
            {
                ctx.Report(id, node);
            }
        }
    }

    public static void BaseController(WalkContext ctx, string id) => BaseNamed(ctx, id, ControllerName);

    public static void EnumNameSuffix(WalkContext ctx, string id)
    {
        Suffix(ctx, id, FlagsSuffix);
        Suffix(ctx, id, EnumSuffix);
    }

    public static void SuffixFlags(WalkContext ctx, string id) => Suffix(ctx, id, FlagsSuffix);

    public static void SuffixEnum(WalkContext ctx, string id) => Suffix(ctx, id, EnumSuffix);

    public static void MissingExceptionSuffix(WalkContext ctx, string id) =>
        MissingSuffix(ctx, id, ExceptionName);

    public static void AsyncSuffix(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.MethodDeclaration, MissingAsyncSuffix);

    private static void CatchNamed(WalkContext ctx, string id, string name)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.CatchClause))
        {
            if (CatchFacts.CatchType(node) == name)
            {
                ctx.Report(id, node);
            }
        }
    }

    private static void ThrowNamed(WalkContext ctx, string id, string name)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.ThrowStatement))
        {
            if (CatchFacts.ThrownType(node) == name)
            {
                ctx.Report(id, node);
            }
        }
    }

    private static void BaseNamed(WalkContext ctx, string id, string name)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.ClassDeclaration))
        {
            if (TypeFacts.Extends(node, name))
            {
                ctx.Report(id, node);
            }
        }
    }

    private static void Suffix(WalkContext ctx, string id, string suffix)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.EnumDeclaration))
        {
            if (TypeFacts.EndsWith(node, suffix))
            {
                ctx.Report(id, node);
            }
        }
    }

    private static void MissingSuffix(WalkContext ctx, string id, string suffix)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.ClassDeclaration))
        {
            if (TypeFacts.Extends(node, suffix) && !TypeFacts.EndsWith(node, suffix))
            {
                ctx.Report(id, node);
            }
        }
    }

    private static void Report(WalkContext ctx, string id, SyntaxKind kind, Func<SyntaxNode, bool> match)
    {
        foreach (var node in ctx.Nodes(kind))
        {
            if (match(node))
            {
                ctx.Report(id, node);
            }
        }
    }

    private static void ReportAny(WalkContext ctx, string id, Func<SyntaxNode, bool> match)
    {
        foreach (var node in ctx.Tree.GetRoot().DescendantNodes())
        {
            if (match(node))
            {
                ctx.Report(id, node);
            }
        }
    }

    private static void ReportComments(WalkContext ctx, string id, Func<SyntaxTrivia, bool> match)
    {
        foreach (var token in ctx.Tree.GetRoot().DescendantTokens())
        {
            ReportTrivia(ctx, id, match, token.LeadingTrivia);
            ReportTrivia(ctx, id, match, token.TrailingTrivia);
        }
    }

    private static void ReportTrivia(
        WalkContext ctx,
        string id,
        Func<SyntaxTrivia, bool> match,
        SyntaxTriviaList trivia)
    {
        foreach (var item in trivia)
        {
            if (match(item))
            {
                ctx.Report(id, item.GetLocation());
            }
        }
    }

    private static void ReportLines(WalkContext ctx, string id, Func<string, bool> match)
    {
        var line = 0;
        foreach (var text in ctx.Text.Split('\n'))
        {
            line++;
            if (match(text))
            {
                ctx.Report(id, ctx.Tree.GetRoot().FindToken(Offset(ctx.Text, line)).GetLocation());
            }
        }
    }

    private static bool IsEmptyType(SyntaxNode node) => Shapes.MemberCount(node) == 0;

    private static bool IsEmptyBody(SyntaxNode node)
    {
        var body = Shapes.Body(node);
        return body is not null && Shapes.IsEmptyBlock(body);
    }

    private static bool NameNotPascal(SyntaxNode node)
    {
        var name = Names.Declared(node);
        return name.Length > 0 && !TypeFacts.IsPascal(name);
    }

    private const string MemoryStreamName = "MemoryStream";

    private static bool IsCreatedDisposable(WalkContext ctx, SyntaxNode node) =>
        Symbols.IsDisposable(ctx, node) || Names.Creation(node) == MemoryStreamName;

    private static bool IsUsing(SyntaxNode node)
    {
        if (Shapes.NestedIn(node, SyntaxKind.UsingStatement))
        {
            return true;
        }

        var local = Shapes.Enclosing(node, SyntaxKind.LocalDeclarationStatement);
        return local is not null && Shapes.HasModifier(local, SyntaxKind.UsingKeyword);
    }

    private static bool IsNullDeref(SyntaxNode node)
    {
        if (node is not IfStatementSyntax statement)
        {
            return false;
        }

        var name = NullName(statement.Condition);
        if (name.Length == 0 || statement.Statement is not BlockSyntax block)
        {
            return false;
        }

        const string Dot = ".";
        return block.ToString().Contains($"{name}{Dot}", StringComparison.Ordinal);
    }

    private static string NullName(ExpressionSyntax expression)
    {
        if (expression is not BinaryExpressionSyntax binary || !binary.IsKind(SyntaxKind.EqualsExpression))
        {
            return string.Empty;
        }

        if (binary.Right is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return Names.Simple(binary.Left);
        }

        return string.Empty;
    }

    private static bool ForUsesParameter(SyntaxNode node)
    {
        if (node is not ForStatementSyntax statement || statement.Condition is null)
        {
            return false;
        }

        foreach (var name in statement.Condition.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (IsDirectParameter(name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDirectParameter(IdentifierNameSyntax name) =>
        IsParameterName(name) && name.Parent is not MemberAccessExpressionSyntax;

    private static bool ArrayUsesParameter(SyntaxNode node)
    {
        if (node is not ArrayCreationExpressionSyntax creation)
        {
            return false;
        }

        foreach (var name in creation.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (IsParameterName(name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsParameterName(IdentifierNameSyntax identifier)
    {
        var method = Shapes.Enclosing(identifier, SyntaxKind.MethodDeclaration) as MethodDeclarationSyntax;
        if (method is null)
        {
            return false;
        }

        foreach (var parameter in method.ParameterList.Parameters)
        {
            if (parameter.Identifier.Text == identifier.Identifier.Text)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLowerSuffix(string text) =>
        text.EndsWith('l') || text.EndsWith('m') || text.EndsWith('d') || text.EndsWith('f') || text.EndsWith('u');

    private static bool ReturnsSameLiteral(SyntaxNode node)
    {
        if (node is not MethodDeclarationSyntax method || method.Body is null)
        {
            return false;
        }

        string? literal = null;
        var returns = 0;
        foreach (var child in method.Body.DescendantNodes().OfType<ReturnStatementSyntax>())
        {
            if (child.Expression is not LiteralExpressionSyntax value)
            {
                return false;
            }

            returns++;
            literal ??= value.Token.Text;
            if (literal != value.Token.Text)
            {
                return false;
            }
        }

        return returns > 1;
    }

    private static bool MissingElse(SyntaxNode node)
    {
        if (node is not IfStatementSyntax statement || statement.Else is null)
        {
            return false;
        }

        if (statement.Parent is ElseClauseSyntax)
        {
            return false;
        }

        return EndsWithIf(statement.Else);
    }

    private static bool EndsWithIf(ElseClauseSyntax clause)
    {
        if (clause.Statement is IfStatementSyntax nested)
        {
            return nested.Else is null || EndsWithIf(nested.Else);
        }

        return false;
    }

    private static bool SameBranch(SyntaxNode node)
    {
        if (node is not IfStatementSyntax statement || statement.Else is null)
        {
            return false;
        }

        return statement.Statement.ToString() == statement.Else.Statement.ToString();
    }

    private const string ForPrefix = "for (";

    private const string GetAccessor = "get;";

    private const string SetAccessor = "set;";

    private static bool HasTwoStatements(string line)
    {
        if (line.Contains(ForPrefix, StringComparison.Ordinal) || line.Contains(GetAccessor, StringComparison.Ordinal))
        {
            return false;
        }

        if (line.Contains(SetAccessor, StringComparison.Ordinal))
        {
            return false;
        }

        var semis = 0;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == ';' && (i == 0 || line[i - 1] != '\''))
            {
                semis++;
            }
        }

        return semis > 1;
    }

    private static bool PublicCtorOnAbstract(SyntaxNode node)
    {
        if (!Shapes.HasModifier(node, SyntaxKind.PublicKeyword))
        {
            return false;
        }

        var type = Shapes.Enclosing(node, SyntaxKind.ClassDeclaration);
        return type is not null && Shapes.HasModifier(type, SyntaxKind.AbstractKeyword);
    }

    private static bool ProtectedOnSealed(SyntaxNode node)
    {
        if (!Shapes.HasModifier(node, SyntaxKind.ProtectedKeyword))
        {
            return false;
        }

        var type = Shapes.Enclosing(node, SyntaxKind.ClassDeclaration);
        return type is not null && Shapes.HasModifier(type, SyntaxKind.SealedKeyword);
    }

    private static bool BodyThrows(SyntaxNode node)
    {
        var body = Shapes.Body(node);
        return body is not null && body.DescendantNodes().Any(child => child.IsKind(SyntaxKind.ThrowStatement));
    }

    private static bool MissingAssert(SyntaxNode node)
    {
        if (!HasAttribute(node, FactName) && !HasAttribute(node, TestName))
        {
            return false;
        }

        var body = Shapes.Body(node);
        return body is not null && !body.ToString().Contains(AssertName, StringComparison.Ordinal);
    }

    private static bool IsIgnore(SyntaxNode node)
    {
        var name = Names.Attribute(node);
        if (name == IgnoreName)
        {
            return true;
        }

        return name == FactName && node.ToString().Contains(SkipName, StringComparison.Ordinal);
    }

    private static bool MissingTest(SyntaxNode node)
    {
        if (!TypeFacts.EndsWith(node, TestsSuffix) && !TypeFacts.EndsWith(node, TestSuffix))
        {
            return false;
        }

        foreach (var child in node.DescendantNodes())
        {
            if (Names.Attribute(child) is FactName or TestName or TestMethodName)
            {
                return false;
            }
        }

        return true;
    }

    private static bool BadTestSignature(SyntaxNode node)
    {
        if (!HasAttribute(node, FactName) && !HasAttribute(node, TestName))
        {
            return false;
        }

        if (node is not MethodDeclarationSyntax method)
        {
            return false;
        }

        return Names.TypeText(method.ReturnType) != VoidName || Shapes.ParameterCount(method) > 0;
    }

    private static bool MissingAsyncSuffix(SyntaxNode node)
    {
        if (!Shapes.HasModifier(node, SyntaxKind.AsyncKeyword))
        {
            return false;
        }

        var name = Names.Declared(node);
        return name.Length > 0 && !name.EndsWith(AsyncSuffixName, StringComparison.Ordinal);
    }

    private static bool HasAttribute(SyntaxNode node, string name)
    {
        foreach (var child in node.ChildNodes())
        {
            if (ListHas(child, name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ListHas(SyntaxNode node, string name)
    {
        if (node is not AttributeListSyntax list)
        {
            return false;
        }

        foreach (var attribute in list.Attributes)
        {
            if (Names.Attribute(attribute) == name)
            {
                return true;
            }
        }

        return false;
    }

    private static int Offset(string text, int line)
    {
        var current = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (current == line)
            {
                return i;
            }

            if (text[i] == '\n')
            {
                current++;
            }
        }

        return 0;
    }
}

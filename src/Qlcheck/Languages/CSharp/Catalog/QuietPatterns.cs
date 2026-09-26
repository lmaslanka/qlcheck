// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class QuietPatterns
{
    private const string ObjectName = "object";

    private const string ZeroText = "0";

    private const string ThisCall = "(this)";

    private const string EqualsOp = "==";

    private const string NotEqualsOp = "!=";

    private const string ReadOnlyWord = "readonly";

    private const string ListName = "List";

    private const string HashSetName = "HashSet";

    private const string DictionaryName = "Dictionary";

    private const string ArraySuffix = "[]";

    private const string Shift32 = "32";

    private const string Shift64 = "64";

    private const string FunctionAttribute = "FunctionName";

    private const int MinReturns = 2;

    private const string EqualsName = "Equals";

    private const string HashName = "GetHashCode";

    public static void ModuloEquals(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.EqualsExpression, IsModulo);

    public static void AssignInExpr(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.SimpleAssignmentExpression, AssignNotStatement);

    public static void CompareThenAssign(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.IfStatement, ChecksThenAssigns);

    public static void DoublePrefix(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.LogicalNotExpression, NestedNot);

    public static void FakeOperator(WalkContext ctx, string id) =>
        FlagTokens(ctx, id, PlusAfterEquals);

    public static void NewHides(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.MethodDeclaration, HasNew);

    public static void EmptyCatch(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.CatchClause, CatchEmpty);

    public static void IncrementArg(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.PostIncrementExpression, InsideCall);

    public static void NullAndIs(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.LogicalAndExpression, NullAndType);

    public static void GenericNull(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.EqualsExpression, GenericComparedToNull);

    public static void UncheckedBlock(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.UncheckedStatement, static _ => true);

    public static void ExtendsObject(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.ClassDeclaration, BaseIsObject);

    public static void EmptyCtor(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.ConstructorDeclaration, CtorEmpty);

    public static void ZeroInit(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.EqualsValueClause, InitsZero);

    public static void ConsecutiveReturn(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.Block, TwoReturns);

    public static void RefParam(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.Parameter, HasRef);

    public static void ReturnUsing(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.ReturnStatement, InsideUsing);

    public static void ThisEscapes(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.ConstructorDeclaration, PassesThis);

    public static void ThrowFinally(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.FinallyClause, ContainsThrow);

    public static void ThrowGetter(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.GetAccessorDeclaration, ContainsThrow);

    public static void ThrowEquals(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.MethodDeclaration, EqualsThrows);

    public static void UselessBit(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.BitwiseAndExpression, SameSides);

    public static void UselessCompare(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.LessThanOrEqualExpression, SameSides);

    public static void UselessIncrement(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.SimpleAssignmentExpression, AssignsIncrement);

    public static void VirtualEvent(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.EventFieldDeclaration, HasVirtual);

    public static void TwoVars(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.VariableDeclaration, ManyVariables);

    public static void OperatorNoName(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.OperatorDeclaration, static _ => true);

    public static void EqualsNoNotEquals(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.ClassDeclaration, EqualsWithoutNot);

    public static void OptionalNotPassed(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.ConstructorDeclaration, BaseSkipsOptional);

    public static void ReadonlyAssign(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.SimpleAssignmentExpression, AssignsReadonlyMember);

    public static void ReadonlyMutable(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.FieldDeclaration, ReadonlyList);

    public static void ReturnsNullArray(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.ReturnStatement, ReturnsNull);

    public static void PrivateUnsealed(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.ClassDeclaration, PrivateNotSealed);

    public static void ShiftFar(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.LeftShiftExpression, ShiftByLarge);

    public static void ShiftNonInt(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.RightShiftExpression, ShiftByName);

    public static void BitAndBool(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.BitwiseAndExpression, BoolOperands);

    public static void AzureState(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.FieldDeclaration, FunctionHasField);

    public static void StaticOrder(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.FieldDeclaration, StaticUsesLater);

    public static void LocalCouldBeConst(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.LocalDeclarationStatement, LiteralNeverWritten);

    public static void InvariantParam(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.TypeParameter, UnusedTypeParam);

    public static void StaticReadonlyLiteral(WalkContext ctx, string id) =>
        Flag(ctx, id, SyntaxKind.FieldDeclaration, StaticReadonlyWithLiteral);

    private static void Flag(WalkContext ctx, string id, SyntaxKind kind, Func<SyntaxNode, bool> match)
    {
        foreach (var node in ctx.Nodes(kind))
        {
            if (match(node))
            {
                ctx.Report(id, node);
            }
        }
    }

    private static void FlagTokens(WalkContext ctx, string id, Func<SyntaxToken, bool> match)
    {
        foreach (var token in ctx.Tree.GetRoot().DescendantTokens())
        {
            if (match(token))
            {
                ctx.Report(id, token);
            }
        }
    }

    private static bool IsModulo(SyntaxNode node) =>
        node is BinaryExpressionSyntax binary && binary.Left.IsKind(SyntaxKind.ModuloExpression);

    private static bool AssignNotStatement(SyntaxNode node) =>
        node.Parent is not (
            ExpressionStatementSyntax
            or EqualsValueClauseSyntax
            or InitializerExpressionSyntax
            or ForStatementSyntax);

    private static bool ChecksThenAssigns(SyntaxNode node)
    {
        if (node is not IfStatementSyntax statement || statement.Condition is not BinaryExpressionSyntax binary)
        {
            return false;
        }

        if (statement.Statement is not ExpressionStatementSyntax expression)
        {
            return false;
        }

        if (expression.Expression is not AssignmentExpressionSyntax assignment)
        {
            return false;
        }

        return assignment.Left.ToString() == binary.Left.ToString()
            && assignment.Right.ToString() == binary.Right.ToString();
    }

    private static bool NestedNot(SyntaxNode node) =>
        node is PrefixUnaryExpressionSyntax prefix && prefix.Operand.IsKind(SyntaxKind.LogicalNotExpression);

    private static bool PlusAfterEquals(SyntaxToken token)
    {
        if (!token.IsKind(SyntaxKind.EqualsToken))
        {
            return false;
        }

        var next = token.GetNextToken();
        return next.IsKind(SyntaxKind.PlusToken) && next.SpanStart == token.Span.End;
    }

    private static bool HasNew(SyntaxNode node) => Shapes.HasModifier(node, SyntaxKind.NewKeyword);

    private static bool CatchEmpty(SyntaxNode node) =>
        node is CatchClauseSyntax clause && clause.Block.Statements.Count == 0;

    private static bool InsideCall(SyntaxNode node) => Shapes.Enclosing(node, SyntaxKind.InvocationExpression) is not null;

    private static bool NullAndType(SyntaxNode node)
    {
        if (node is not BinaryExpressionSyntax binary)
        {
            return false;
        }

        var equalsNull = binary.DescendantNodesAndSelf().OfType<BinaryExpressionSyntax>()
            .Any(item => item.IsKind(SyntaxKind.EqualsExpression)
                && (item.Left.IsKind(SyntaxKind.NullLiteralExpression)
                    || item.Right.IsKind(SyntaxKind.NullLiteralExpression)));
        var isType = binary.DescendantNodes().Any(item => item.IsKind(SyntaxKind.IsExpression));
        return equalsNull && isType;
    }

    private static bool GenericComparedToNull(SyntaxNode node)
    {
        if (node is not BinaryExpressionSyntax binary)
        {
            return false;
        }

        var method = Shapes.Enclosing(node, SyntaxKind.MethodDeclaration) as MethodDeclarationSyntax;
        if (method?.TypeParameterList is null)
        {
            return false;
        }

        var names = method.ParameterList.Parameters
            .Where(parameter => method.TypeParameterList.Parameters.Any(
                type => type.Identifier.Text == Names.TypeText(parameter.Type!)))
            .Select(parameter => parameter.Identifier.Text)
            .ToHashSet(StringComparer.Ordinal);
        return names.Contains(Names.Simple(binary.Left)) && binary.Right.IsKind(SyntaxKind.NullLiteralExpression);
    }

    private static bool BaseIsObject(SyntaxNode node) => TypeFacts.BaseType(node) == ObjectName;

    private static bool CtorEmpty(SyntaxNode node)
    {
        var body = Shapes.Body(node);
        return body is not null && Shapes.IsEmptyBlock(body) && Shapes.HasModifier(node, SyntaxKind.PublicKeyword);
    }

    private static bool InitsZero(SyntaxNode node)
    {
        if (node is not EqualsValueClauseSyntax clause)
        {
            return false;
        }

        if (clause.Value is not LiteralExpressionSyntax literal || literal.Token.ValueText != ZeroText)
        {
            return false;
        }

        var member = Shapes.Enclosing(node, SyntaxKind.FieldDeclaration);
        return member is not null && !Shapes.HasModifier(member, SyntaxKind.ConstKeyword);
    }

    private static bool TwoReturns(SyntaxNode node)
    {
        if (node is not BlockSyntax block || block.Statements.Count < MinReturns)
        {
            return false;
        }

        for (var i = 1; i < block.Statements.Count; i++)
        {
            if (block.Statements[i] is ReturnStatementSyntax && block.Statements[i - 1] is ReturnStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasRef(SyntaxNode node) =>
        node is ParameterSyntax parameter && parameter.Modifiers.Any(SyntaxKind.RefKeyword);

    private static bool InsideUsing(SyntaxNode node) => Shapes.NestedIn(node, SyntaxKind.UsingStatement);

    private static bool PassesThis(SyntaxNode node) => node.ToString().Contains(ThisCall, StringComparison.Ordinal);

    private static bool ContainsThrow(SyntaxNode node) =>
        node.DescendantNodes().Any(child => child.IsKind(SyntaxKind.ThrowStatement));

    private static bool EqualsThrows(SyntaxNode node) =>
        Names.Declared(node) is EqualsName or HashName && ContainsThrow(node);

    private static bool SameSides(SyntaxNode node) =>
        node is BinaryExpressionSyntax binary && binary.Left.ToString() == binary.Right.ToString();

    private static bool AssignsIncrement(SyntaxNode node)
    {
        if (node is not AssignmentExpressionSyntax assignment)
        {
            return false;
        }

        return assignment.Right.IsKind(SyntaxKind.PostIncrementExpression)
            && assignment.Right.ToString().StartsWith(assignment.Left.ToString(), StringComparison.Ordinal);
    }

    private static bool HasVirtual(SyntaxNode node) => Shapes.HasModifier(node, SyntaxKind.VirtualKeyword);

    private static bool ManyVariables(SyntaxNode node) =>
        node is VariableDeclarationSyntax declaration && declaration.Variables.Count > 1;

    private static bool EqualsWithoutNot(SyntaxNode node)
    {
        var ops = node.ChildNodes().OfType<OperatorDeclarationSyntax>().Select(op => op.OperatorToken.Text).ToList();
        return ops.Contains(EqualsOp) && !ops.Contains(NotEqualsOp);
    }

    private static bool BaseSkipsOptional(SyntaxNode node)
    {
        if (node is not ConstructorDeclarationSyntax ctor || ctor.Initializer is null)
        {
            return false;
        }

        return ctor.ParameterList.Parameters.Any(parameter => parameter.Default is not null)
            && ctor.Initializer.ArgumentList.Arguments.Count < ctor.ParameterList.Parameters.Count;
    }

    private static bool AssignsReadonlyMember(SyntaxNode node)
    {
        if (node is not AssignmentExpressionSyntax assignment || assignment.Left is not MemberAccessExpressionSyntax)
        {
            return false;
        }

        var type = Shapes.Enclosing(node, SyntaxKind.ClassDeclaration);
        return type is not null && type.ToString().Contains(ReadOnlyWord, StringComparison.Ordinal);
    }

    private static bool ReadonlyList(SyntaxNode node)
    {
        if (!Shapes.HasModifier(node, SyntaxKind.ReadOnlyKeyword) || Shapes.HasModifier(node, SyntaxKind.PrivateKeyword))
        {
            return false;
        }

        if (node is not FieldDeclarationSyntax member)
        {
            return false;
        }

        var name = Names.TypeText(member.Declaration.Type);
        return name is ListName or HashSetName or DictionaryName;
    }

    private static bool ReturnsNull(SyntaxNode node) =>
        node is ReturnStatementSyntax statement && statement.Expression is not null
        && statement.Expression.IsKind(SyntaxKind.NullLiteralExpression)
        && Shapes.Enclosing(node, SyntaxKind.MethodDeclaration) is MethodDeclarationSyntax method
        && method.ReturnType.ToString().Contains(ArraySuffix, StringComparison.Ordinal);

    private static bool PrivateNotSealed(SyntaxNode node) =>
        Shapes.HasModifier(node, SyntaxKind.PrivateKeyword)
        && !Shapes.HasModifier(node, SyntaxKind.SealedKeyword)
        && node.IsKind(SyntaxKind.ClassDeclaration);

    private static bool ShiftByLarge(SyntaxNode node) =>
        node is BinaryExpressionSyntax binary
        && binary.Right is LiteralExpressionSyntax literal
        && literal.Token.ValueText is Shift32 or Shift64;

    private static bool ShiftByName(SyntaxNode node) =>
        node is BinaryExpressionSyntax binary && binary.Right is IdentifierNameSyntax;

    private static bool BoolOperands(SyntaxNode node) =>
        node is BinaryExpressionSyntax binary
        && binary.Left.IsKind(SyntaxKind.IdentifierName)
        && binary.Right.IsKind(SyntaxKind.IdentifierName);

    private static bool FunctionHasField(SyntaxNode node)
    {
        var type = Shapes.Enclosing(node, SyntaxKind.ClassDeclaration);
        return type is not null && type.DescendantNodes().Any(child => Names.Attribute(child) == FunctionAttribute);
    }

    private static bool StaticUsesLater(SyntaxNode node)
    {
        if (!Shapes.HasModifier(node, SyntaxKind.StaticKeyword) || node is not FieldDeclarationSyntax member)
        {
            return false;
        }

        var name = member.Declaration.Variables[0].Identifier.Text;
        var type = Shapes.Enclosing(node, SyntaxKind.ClassDeclaration);
        if (type is null)
        {
            return false;
        }

        var later = false;
        foreach (var child in type.ChildNodes().OfType<FieldDeclarationSyntax>())
        {
            if (child == node)
            {
                later = true;
                continue;
            }

            if (!later && child.ToString().Contains(name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LiteralNeverWritten(SyntaxNode node)
    {
        if (node is not LocalDeclarationStatementSyntax local || Shapes.HasModifier(local, SyntaxKind.ConstKeyword))
        {
            return false;
        }

        var variable = local.Declaration.Variables[0];
        if (variable.Initializer?.Value is not LiteralExpressionSyntax)
        {
            return false;
        }

        var method = Shapes.Enclosing(node, SyntaxKind.MethodDeclaration);
        if (method is null)
        {
            return false;
        }

        var name = variable.Identifier.Text;
        foreach (var child in method.DescendantNodes())
        {
            if (child is AssignmentExpressionSyntax assignment && Names.Simple(assignment.Left) == name)
            {
                return false;
            }

            if (child is PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax
                && child.ToString().Contains(name, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool UnusedTypeParam(SyntaxNode node)
    {
        if (node is not TypeParameterSyntax parameter)
        {
            return false;
        }

        var parent = parameter.Parent?.Parent;
        if (parent is null)
        {
            return false;
        }

        var name = parameter.Identifier.Text;
        var uses = parent.DescendantTokens().Count(token => token.Text == name);
        return uses == 1;
    }

    private static bool StaticReadonlyWithLiteral(SyntaxNode node)
    {
        if (!Shapes.HasModifier(node, SyntaxKind.StaticKeyword) || !Shapes.HasModifier(node, SyntaxKind.ReadOnlyKeyword))
        {
            return false;
        }

        if (node is not FieldDeclarationSyntax member)
        {
            return false;
        }

        return member.Declaration.Variables.Any(variable => variable.Initializer?.Value is LiteralExpressionSyntax);
    }
}

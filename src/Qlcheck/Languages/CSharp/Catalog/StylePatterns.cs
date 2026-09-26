// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class StylePatterns
{
    private const int LargeDigits = 5;

    private const string IntName = "int";

    private const string StringName = "string";

    private const string ObjectName = "object";

    private const string AddCall = ".Add(";

    private const string FloatName = "float";

    private const string DoubleName = "double";

    private const string ZeroText = "0";

    private const string Decrement = "--";

    private const string ToStringName = "ToString";

    private const string EqualsName = "Equals";

    private const string CreateName = "Create";

    private const string DesName = "DES";

    private const string Md5Name = "MD5";

    private const string Rc2Name = "RC2";

    private const string AnyName = "Any";

    private const string ItemsName = "items";

    private const string EqualsOp = "==";

    private const int MinSwitchSections = 3;

    private const int MinEqualsArgs = 3;

    private const int MinCatches = 2;

    public static void AbstractMixed(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ClassDeclaration, AbstractUnbalanced);

    public static void BraceAtLineStart(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.Block, CloseBraceNotAlone);

    public static void ConditionalOnNewLine(WalkContext ctx, string id) =>
        ReportTokens(ctx, id, EndsCondition);

    public static void PublicConst(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.FieldDeclaration, PublicAndConst);

    public static void VirtualFromCtor(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ConstructorDeclaration, CallsVirtual);

    public static void DefaultMiddle(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.SwitchStatement, DefaultInMiddle);

    public static void DigitSeparator(WalkContext ctx, string id) =>
        ReportTokens(ctx, id, LargePlainNumber);

    public static void ExtensionObject(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.MethodDeclaration, ThisObject);

    public static void AssignedNotReadonly(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.FieldDeclaration, MutableAssignedInCtor);

    public static void SameIf(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.MethodDeclaration, DuplicateIf);

    public static void IndentConditional(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.IfStatement, BracelessUnindented);

    public static void BadIndexer(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.IndexerDeclaration, IndexerNotIntOrString);

    public static void IntDivToFloat(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.VariableDeclarator, IntDivisionToFloat);

    public static void ExplicitInterface(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.MethodDeclaration, IsExplicitInterface);

    public static void ForNeverTrue(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ForStatement, ForConditionNever);

    public static void ForWrongWay(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ForStatement, ForDecrementsWhileLess);

    public static void ForOnce(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ForStatement, ForAtMostOne);

    public static void ForNoCounter(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ForStatement, ForSkipsCounter);

    public static void ForBoundChanges(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ForStatement, ForAssignsBound);

    public static void PreferWhile(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ForStatement, EmptyForClauses);

    public static void PublicOnInternal(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.FieldDeclaration, PublicOnNonPublicType);

    public static void SameCatch(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.TryStatement, DuplicateCatch);

    public static void NestedSameIf(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.IfStatement, NestedSameCondition);

    public static void PrivateOnlyCtor(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ClassDeclaration, OnlyPrivateCtors);

    public static void NarrowOverride(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.MethodDeclaration, PrivateOverride);

    public static void MoveToInner(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.MethodDeclaration, PrivateOnlyUsedInNested);

    public static void ParamsAndOverride(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.MethodDeclaration, ParamsOverride);

    public static void PublicStaticField(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.FieldDeclaration, PublicStaticMutable);

    public static void StaticInGeneric(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.FieldDeclaration, StaticFieldOnGeneric);

    public static void StaticWriteInstance(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.MethodDeclaration, InstanceWritesStatic);

    public static void StaticWriteCtor(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ConstructorDeclaration, WritesStatic);

    public static void StaticNoInline(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.FieldDeclaration, StaticWithoutInitializer);

    public static void UtilityPublicCtor(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ClassDeclaration, StaticMembersPublicCtor);

    public static void RedundantPublic(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.MethodDeclaration, PublicOnInterface);

    public static void RedundantReturn(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ReturnStatement, ReturnAtEnd);

    public static void BoolCompare(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.EqualsExpression, ComparesBoolLiteral);

    public static void SelfArg(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.InvocationExpression, PassesReceiver);

    public static void ReplaceElement(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.SimpleAssignmentExpression, AssignsElement);

    public static void ForeachAdd(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ForEachStatement, OnlyAdds);

    public static void UninvokedEvent(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.EventFieldDeclaration, EventNotInvoked);

    public static void StringToString(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.InvocationExpression, ToStringOnString);

    public static void EqualsWithoutComparison(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.InvocationExpression, EqualsMissingComparison);

    public static void CipherCreate(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.InvocationExpression, CreatesWeakCipher);

    public static void AnyEquals(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.InvocationExpression, AnySimpleEquals);

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

    private static void ReportTokens(WalkContext ctx, string id, Func<SyntaxToken, bool> match)
    {
        foreach (var token in ctx.Tree.GetRoot().DescendantTokens())
        {
            if (match(token))
            {
                ctx.Report(id, token);
            }
        }
    }

    private static bool AbstractUnbalanced(SyntaxNode node)
    {
        if (!Shapes.HasModifier(node, SyntaxKind.AbstractKeyword))
        {
            return false;
        }

        var methods = node.ChildNodes().Where(child => child.IsKind(SyntaxKind.MethodDeclaration)).ToList();
        if (methods.Count == 0)
        {
            return false;
        }

        var abstractCount = methods.Count(method => Shapes.HasModifier(method, SyntaxKind.AbstractKeyword));
        return abstractCount == 0 || abstractCount == methods.Count;
    }

    private static bool CloseBraceNotAlone(SyntaxNode node)
    {
        if (node is not BlockSyntax block)
        {
            return false;
        }

        var line = LineText(block.CloseBraceToken);
        return line.TrimStart().Length > 1;
    }

    private static bool EndsCondition(SyntaxToken token)
    {
        if (!token.IsKind(SyntaxKind.AmpersandAmpersandToken) && !token.IsKind(SyntaxKind.BarBarToken))
        {
            return false;
        }

        var line = LineText(token);
        return line.TrimEnd().EndsWith(token.Text, StringComparison.Ordinal);
    }

    private static bool PublicAndConst(SyntaxNode node) =>
        Shapes.HasModifier(node, SyntaxKind.PublicKeyword) && Shapes.HasModifier(node, SyntaxKind.ConstKeyword);

    private static bool CallsVirtual(SyntaxNode node)
    {
        var type = Shapes.Enclosing(node, SyntaxKind.ClassDeclaration);
        if (type is null)
        {
            return false;
        }

        var virtuals = type.ChildNodes()
            .Where(child => child.IsKind(SyntaxKind.MethodDeclaration) && Shapes.HasModifier(child, SyntaxKind.VirtualKeyword))
            .Select(Names.Declared)
            .ToHashSet(StringComparer.Ordinal);
        return node.DescendantNodes().Any(child => virtuals.Contains(Names.Invocation(child)));
    }

    private static bool DefaultInMiddle(SyntaxNode node)
    {
        if (node is not SwitchStatementSyntax statement || statement.Sections.Count < MinSwitchSections)
        {
            return false;
        }

        var index = 0;
        var found = -1;
        foreach (var section in statement.Sections)
        {
            if (section.Labels.Any(label => label.IsKind(SyntaxKind.DefaultSwitchLabel)))
            {
                found = index;
            }

            index++;
        }

        return found > 0 && found < statement.Sections.Count - 1;
    }

    private static bool LargePlainNumber(SyntaxToken token)
    {
        if (!token.IsKind(SyntaxKind.NumericLiteralToken) || token.Text.Contains('_'))
        {
            return false;
        }

        var digits = 0;
        foreach (var ch in token.Text)
        {
            if (char.IsDigit(ch))
            {
                digits++;
            }
        }

        return digits >= LargeDigits;
    }

    private static bool ThisObject(SyntaxNode node)
    {
        if (node is not MethodDeclarationSyntax method)
        {
            return false;
        }

        foreach (var parameter in method.ParameterList.Parameters)
        {
            if (parameter.Modifiers.Any(SyntaxKind.ThisKeyword) && Names.TypeText(parameter.Type!) == ObjectName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool MutableAssignedInCtor(SyntaxNode node)
    {
        if (Shapes.HasModifier(node, SyntaxKind.ReadOnlyKeyword) || node is not FieldDeclarationSyntax member)
        {
            return false;
        }

        var name = member.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? string.Empty;
        var type = Shapes.Enclosing(node, SyntaxKind.ClassDeclaration);
        if (type is null || name.Length == 0)
        {
            return false;
        }

        var ctor = type.ChildNodes().FirstOrDefault(child => child.IsKind(SyntaxKind.ConstructorDeclaration));
        return ctor is not null && ctor.ToString().Contains($"{name} =", StringComparison.Ordinal);
    }

    private static bool DuplicateIf(SyntaxNode node)
    {
        var conditions = node.DescendantNodes()
            .OfType<IfStatementSyntax>()
            .Select(statement => statement.Condition.ToString())
            .ToList();
        return conditions.Count != conditions.Distinct(StringComparer.Ordinal).Count();
    }

    private static bool BracelessUnindented(SyntaxNode node)
    {
        if (node is not IfStatementSyntax statement || statement.Statement is BlockSyntax)
        {
            return false;
        }

        return statement.Statement.GetLocation().GetLineSpan().StartLinePosition.Character
            <= statement.GetLocation().GetLineSpan().StartLinePosition.Character;
    }

    private static bool IndexerNotIntOrString(SyntaxNode node)
    {
        if (node is not IndexerDeclarationSyntax indexer)
        {
            return false;
        }

        var type = Names.TypeText(indexer.ParameterList.Parameters[0].Type!);
        return type is not (IntName or StringName);
    }

    private static bool IntDivisionToFloat(SyntaxNode node)
    {
        if (node is not VariableDeclaratorSyntax declarator || declarator.Initializer is null)
        {
            return false;
        }

        var parent = declarator.Parent as VariableDeclarationSyntax;
        if (parent is null || Names.TypeText(parent.Type) is not (FloatName or DoubleName))
        {
            return false;
        }

        return declarator.Initializer.Value is BinaryExpressionSyntax binary
            && binary.IsKind(SyntaxKind.DivideExpression);
    }

    private static bool IsExplicitInterface(SyntaxNode node) =>
        node is MethodDeclarationSyntax method && method.ExplicitInterfaceSpecifier is not null;

    private static bool ForConditionNever(SyntaxNode node) =>
        node is ForStatementSyntax statement && statement.Condition is BinaryExpressionSyntax binary
        && binary.IsKind(SyntaxKind.LessThanExpression)
        && binary.Right is LiteralExpressionSyntax right
        && right.Token.ValueText == ZeroText;

    private static bool ForDecrementsWhileLess(SyntaxNode node)
    {
        if (node is not ForStatementSyntax statement || statement.Incrementors.Count == 0)
        {
            return false;
        }

        return statement.Condition is BinaryExpressionSyntax binary
            && binary.IsKind(SyntaxKind.LessThanExpression)
            && statement.Incrementors[0].ToString().Contains(Decrement, StringComparison.Ordinal);
    }

    private static bool ForAtMostOne(SyntaxNode node) =>
        node is ForStatementSyntax statement
        && statement.Condition is BinaryExpressionSyntax binary
        && binary.Right is LiteralExpressionSyntax right
        && right.Token.ValueText == ZeroText;

    private static bool ForSkipsCounter(SyntaxNode node)
    {
        if (node is not ForStatementSyntax statement || statement.Declaration is null || statement.Incrementors.Count == 0)
        {
            return false;
        }

        var counter = statement.Declaration.Variables[0].Identifier.Text;
        return !statement.Incrementors[0].ToString().Contains(counter, StringComparison.Ordinal);
    }

    private static bool ForAssignsBound(SyntaxNode node)
    {
        if (node is not ForStatementSyntax statement || statement.Condition is not BinaryExpressionSyntax binary)
        {
            return false;
        }

        var bound = Names.Simple(binary.Right);
        return bound.Length > 0 && statement.Statement.ToString().Contains($"{bound} =", StringComparison.Ordinal);
    }

    private static bool EmptyForClauses(SyntaxNode node) =>
        node is ForStatementSyntax statement && statement.Declaration is null && statement.Incrementors.Count == 0;

    private static bool PublicOnNonPublicType(SyntaxNode node)
    {
        if (!Shapes.HasModifier(node, SyntaxKind.PublicKeyword))
        {
            return false;
        }

        if (node is not FieldDeclarationSyntax)
        {
            return false;
        }

        var type = Shapes.Enclosing(node, SyntaxKind.ClassDeclaration);
        return type is not null && !Shapes.HasModifier(type, SyntaxKind.PublicKeyword);
    }

    private static bool DuplicateCatch(SyntaxNode node)
    {
        if (node is not TryStatementSyntax statement || statement.Catches.Count < MinCatches)
        {
            return false;
        }

        var first = statement.Catches[0].Block.ToString();
        return statement.Catches.Skip(1).Any(clause => clause.Block.ToString() == first);
    }

    private static bool NestedSameCondition(SyntaxNode node)
    {
        if (node is not IfStatementSyntax statement || statement.Statement is not BlockSyntax block)
        {
            return false;
        }

        var inner = block.Statements.OfType<IfStatementSyntax>().FirstOrDefault();
        return inner is not null && inner.Condition.ToString() == statement.Condition.ToString();
    }

    private static bool OnlyPrivateCtors(SyntaxNode node)
    {
        var ctors = node.ChildNodes().Where(child => child.IsKind(SyntaxKind.ConstructorDeclaration)).ToList();
        return ctors.Count > 0 && ctors.All(ctor => Shapes.HasModifier(ctor, SyntaxKind.PrivateKeyword));
    }

    private static bool PrivateOverride(SyntaxNode node) =>
        Shapes.HasModifier(node, SyntaxKind.PrivateKeyword) && Shapes.HasModifier(node, SyntaxKind.OverrideKeyword);

    private static bool PrivateOnlyUsedInNested(SyntaxNode node)
    {
        if (!Shapes.HasModifier(node, SyntaxKind.PrivateKeyword) || node is not MethodDeclarationSyntax)
        {
            return false;
        }

        var type = Shapes.Enclosing(node, SyntaxKind.ClassDeclaration);
        if (type is null || !type.ChildNodes().Any(child => child.IsKind(SyntaxKind.ClassDeclaration)))
        {
            return false;
        }

        var name = Names.Declared(node);
        var uses = type.DescendantTokens().Count(token => token.Text == name);
        var nested = type.ChildNodes().Where(child => child.IsKind(SyntaxKind.ClassDeclaration));
        var nestedUses = nested.SelectMany(child => child.DescendantTokens()).Count(token => token.Text == name);
        return uses > 1 && nestedUses == uses - 1;
    }

    private static bool ParamsOverride(SyntaxNode node) =>
        Shapes.HasModifier(node, SyntaxKind.OverrideKeyword)
        && node is MethodDeclarationSyntax method
        && method.ParameterList.Parameters.Any(parameter => parameter.Modifiers.Any(SyntaxKind.ParamsKeyword));

    private static bool PublicStaticMutable(SyntaxNode node) =>
        Shapes.HasModifier(node, SyntaxKind.PublicKeyword)
        && Shapes.HasModifier(node, SyntaxKind.StaticKeyword)
        && !Shapes.HasModifier(node, SyntaxKind.ConstKeyword)
        && !Shapes.HasModifier(node, SyntaxKind.ReadOnlyKeyword);

    private static bool StaticFieldOnGeneric(SyntaxNode node)
    {
        if (!Shapes.HasModifier(node, SyntaxKind.StaticKeyword) || node is not FieldDeclarationSyntax)
        {
            return false;
        }

        var type = Shapes.Enclosing(node, SyntaxKind.ClassDeclaration) as TypeDeclarationSyntax;
        return type?.TypeParameterList is not null;
    }

    private static bool InstanceWritesStatic(SyntaxNode node)
    {
        if (Shapes.HasModifier(node, SyntaxKind.StaticKeyword))
        {
            return false;
        }

        return WritesStatic(node);
    }

    private static bool WritesStatic(SyntaxNode node)
    {
        var type = Shapes.Enclosing(node, SyntaxKind.ClassDeclaration);
        if (type is null)
        {
            return false;
        }

        var fields = type.ChildNodes()
            .OfType<FieldDeclarationSyntax>()
            .Where(member => Shapes.HasModifier(member, SyntaxKind.StaticKeyword))
            .SelectMany(member => member.Declaration.Variables.Select(variable => variable.Identifier.Text))
            .ToHashSet(StringComparer.Ordinal);
        return node.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Any(assignment => fields.Contains(Names.Simple(assignment.Left)));
    }

    private static bool StaticWithoutInitializer(SyntaxNode node)
    {
        if (!Shapes.HasModifier(node, SyntaxKind.StaticKeyword) || node is not FieldDeclarationSyntax member)
        {
            return false;
        }

        return member.Declaration.Variables.Any(variable => variable.Initializer is null);
    }

    private static bool StaticMembersPublicCtor(SyntaxNode node)
    {
        var methods = node.ChildNodes().OfType<MethodDeclarationSyntax>().ToList();
        if (methods.Count == 0 || methods.Any(method => !Shapes.HasModifier(method, SyntaxKind.StaticKeyword)))
        {
            return false;
        }

        return node.ChildNodes().Any(child =>
            child.IsKind(SyntaxKind.ConstructorDeclaration) && Shapes.HasModifier(child, SyntaxKind.PublicKeyword));
    }

    private static bool PublicOnInterface(SyntaxNode node)
    {
        if (!Shapes.HasModifier(node, SyntaxKind.PublicKeyword))
        {
            return false;
        }

        return Shapes.Enclosing(node, SyntaxKind.InterfaceDeclaration) is not null;
    }

    private static bool ReturnAtEnd(SyntaxNode node)
    {
        if (node.Parent is not BlockSyntax block || block.Statements.Last() != node)
        {
            return false;
        }

        return block.Parent is MethodDeclarationSyntax && node is ReturnStatementSyntax ret && ret.Expression is null;
    }

    private static bool ComparesBoolLiteral(SyntaxNode node)
    {
        if (node is not BinaryExpressionSyntax binary)
        {
            return false;
        }

        return IsBool(binary.Left) || IsBool(binary.Right);
    }

    private static bool IsBool(ExpressionSyntax expression) =>
        expression.IsKind(SyntaxKind.TrueLiteralExpression) || expression.IsKind(SyntaxKind.FalseLiteralExpression);

    private static bool PassesReceiver(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation || invocation.Expression is not MemberAccessExpressionSyntax member)
        {
            return false;
        }

        var receiver = member.Expression.ToString();
        return invocation.ArgumentList.Arguments.Any(argument => argument.Expression.ToString() == receiver);
    }

    private static bool AssignsElement(SyntaxNode node)
    {
        if (node is not AssignmentExpressionSyntax assignment || assignment.Left is not ElementAccessExpressionSyntax access)
        {
            return false;
        }

        foreach (var argument in access.ArgumentList.Arguments)
        {
            if (argument.Expression is LiteralExpressionSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private static bool OnlyAdds(SyntaxNode node)
    {
        if (node is not ForEachStatementSyntax statement || statement.Statement is not BlockSyntax block)
        {
            return false;
        }

        return block.Statements.Count == 1 && block.Statements[0].ToString().Contains(AddCall, StringComparison.Ordinal);
    }

    private static bool EventNotInvoked(SyntaxNode node)
    {
        if (node is not EventFieldDeclarationSyntax member)
        {
            return false;
        }

        var name = member.Declaration.Variables[0].Identifier.Text;
        var type = Shapes.Enclosing(node, SyntaxKind.ClassDeclaration);
        return type is not null && !type.ToString().Contains($"{name}?.Invoke", StringComparison.Ordinal)
            && !type.ToString().Contains($"{name}.Invoke", StringComparison.Ordinal);
    }

    private static bool ToStringOnString(SyntaxNode node) =>
        Names.Invocation(node) == ToStringName
        && node is InvocationExpressionSyntax invocation
        && invocation.Expression is MemberAccessExpressionSyntax member
        && member.Expression is LiteralExpressionSyntax literal
        && literal.IsKind(SyntaxKind.StringLiteralExpression);

    private static bool EqualsMissingComparison(SyntaxNode node)
    {
        if (Names.Invocation(node) != EqualsName || node is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax member)
        {
            return false;
        }

        return member.Expression.ToString() == StringName && invocation.ArgumentList.Arguments.Count < MinEqualsArgs;
    }

    private static bool CreatesWeakCipher(SyntaxNode node) =>
        Names.Invocation(node) == CreateName && Names.InvocationType(node) is DesName or Md5Name or Rc2Name;

    private static bool AnySimpleEquals(SyntaxNode node)
    {
        if (Names.Invocation(node) != AnyName || node is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        return invocation.ArgumentList.Arguments.Count == 1
            && invocation.ArgumentList.Arguments[0].ToString().Contains(EqualsOp, StringComparison.Ordinal)
            && Names.InvocationType(node) == ItemsName;
    }

    private static string LineText(SyntaxToken token)
    {
        var line = token.GetLocation().GetLineSpan().StartLinePosition.Line;
        var lines = token.SyntaxTree!.GetText().Lines;
        return lines[line].ToString();
    }
}

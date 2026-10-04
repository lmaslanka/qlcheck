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

    private const string DataTestMethodName = "DataTestMethod";

    private const string TestCaseName = "TestCase";

    private const string TestCaseSourceName = "TestCaseSource";

    private const string IgnoreName = "Ignore";

    private const string SkipName = "Skip";

    private const string AssertName = "Assert";

    private const string SleepName = "Sleep";

    private const string VoidName = "void";

    private const string LongName = "long";

    private const string FormatMethodName = "Format";

    private const string WriteLineMethodName = "WriteLine";

    private const string ConsoleTypeName = "Console";

    private const string MainMethodName = "Main";

    private const string TheoryName = "Theory";

    private const string TaskTypeName = "Task";

    private const string VerifyMethodName = "Verify";

    private const string ListTypeName = "List";

    private const string GetMethodName = "Get";

    public static void EmptyMethod(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.MethodDeclaration, Shapes.IsEmptyMethod);

    public static void EmptyClass(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.ClassDeclaration, IsEmptyClass);

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
        Report(ctx, id, SyntaxKind.FieldDeclaration, node => TypeFacts.IsPublicField(ctx, node));

    public static void FieldPrivate(WalkContext ctx, string id) =>
        Report(ctx, id, SyntaxKind.FieldDeclaration, TypeFacts.FieldNotPrivate);

    public static void CatchException(WalkContext ctx, string id) => CatchNamed(ctx, id, ExceptionName);

    public static void CatchNull(WalkContext ctx, string id) => CatchNamed(ctx, id, NullReferenceName);

    public static void ThrowException(WalkContext ctx, string id) => ThrowNamed(ctx, id, ExceptionName);

    public static void ThrowNotImplemented(WalkContext ctx, string id) =>
        ThrowNamed(ctx, id, NotImplementedName);

    public static void ExceptionNameExtends(WalkContext ctx, string id)
    {
        foreach (var node in ExceptionNamedTypes(ctx))
        {
            var baseType = TypeFacts.BaseType(node);
            if (baseType.Length == 0 || !baseType.EndsWith(ExceptionName, StringComparison.Ordinal))
            {
                ctx.Report(id, node);
            }
        }
    }

    public static void ExceptionPublic(WalkContext ctx, string id)
    {
        foreach (var node in ExceptionNamedTypes(ctx))
        {
            if (!Shapes.HasModifier(node, SyntaxKind.PublicKeyword))
            {
                ctx.Report(id, node);
            }
        }
    }

    public static void ExceptionStandardConstructors(WalkContext ctx, string id)
    {
        foreach (var node in ExceptionNamedTypes(ctx))
        {
            if (MissingStandardConstructors(node))
            {
                ctx.Report(id, node);
            }
        }
    }

    public static void ThrowCreatedException(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.ObjectCreationExpression).Concat(
            ctx.Nodes(SyntaxKind.ImplicitObjectCreationExpression)))
        {
            if (node.Parent is not ExpressionStatementSyntax)
            {
                continue;
            }

            if (IsExceptionCreation(ctx, node))
            {
                ctx.Report(id, node);
            }
        }
    }

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
            if (IsCreatedDisposable(ctx, node) && !IsUsing(node) && !EscapesOwnership(node))
            {
                ctx.Report(id, node);
            }
        }
    }

    public static void ListInPublicApi(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.MethodDeclaration))
        {
            if (node is not MethodDeclarationSyntax method || !Shapes.HasModifier(method, SyntaxKind.PublicKeyword))
            {
                continue;
            }

            var hasListType = Names.TypeText(method.ReturnType) == ListTypeName
                || method.ParameterList.Parameters.Any(parameter =>
                    parameter.Type is not null && Names.TypeText(parameter.Type) == ListTypeName);
            if (hasListType)
            {
                ctx.Report(id, method);
            }
        }

        foreach (var node in ctx.Nodes(SyntaxKind.PropertyDeclaration))
        {
            if (node is PropertyDeclarationSyntax property
                && Shapes.HasModifier(property, SyntaxKind.PublicKeyword)
                && Names.TypeText(property.Type) == ListTypeName)
            {
                ctx.Report(id, property);
            }
        }
    }

    public static void NestedGenericSignature(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.MethodDeclaration))
        {
            if (node is not MethodDeclarationSyntax method)
            {
                continue;
            }

            var types = method.ParameterList.Parameters
                .Select(parameter => parameter.Type)
                .Append(method.ReturnType)
                .OfType<TypeSyntax>();
            if (types.Any(HasNestedGeneric))
            {
                ctx.Report(id, method);
            }
        }
    }

    private static bool HasNestedGeneric(TypeSyntax type)
    {
        var generic = AsGenericName(type);
        if (generic is null)
        {
            return false;
        }

        // `Task<T>`/`ValueTask<T>`/`ActionResult<T>` are transparent async/framework wrappers, not a
        // "nesting" level in their own right: `Task<ActionResult<IReadOnlyList<T>>>` is the ordinary
        // shape of an async ASP.NET Core collection-returning action, not a nested-generic smell.
        if (generic.Identifier.Text is "Task" or "ValueTask" or "ActionResult" && generic.TypeArgumentList.Arguments.Count == 1)
        {
            return HasNestedGeneric(generic.TypeArgumentList.Arguments[0]);
        }

        return generic.TypeArgumentList.Arguments.Any(argument => AsGenericName(argument) is not null);
    }

    private static GenericNameSyntax? AsGenericName(TypeSyntax type) =>
        type switch
        {
            GenericNameSyntax generic => generic,
            QualifiedNameSyntax qualified => AsGenericName(qualified.Right),
            _ => null,
        };

    public static void ParameterlessGetInvocation(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is InvocationExpressionSyntax invocation
                && Names.Invocation(invocation) == GetMethodName
                && invocation.ArgumentList.Arguments.Count == 0)
            {
                ctx.Report(id, invocation);
            }
        }
    }

    public static void TaskReturnsNull(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.MethodDeclaration))
        {
            if (node is not MethodDeclarationSyntax method
                || Shapes.HasModifier(method, SyntaxKind.AsyncKeyword)
                || Names.TypeText(method.ReturnType) != TaskTypeName)
            {
                continue;
            }

            if (ReturnsNullLiteral(method))
            {
                ctx.Report(id, method);
            }
        }
    }

    private static bool ReturnsNullLiteral(MethodDeclarationSyntax method)
    {
        if (method.ExpressionBody?.Expression is LiteralExpressionSyntax expression
            && expression.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return true;
        }

        if (method.Body is null)
        {
            return false;
        }

        return method.Body.DescendantNodes().OfType<ReturnStatementSyntax>()
            .Any(statement => statement.Expression is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.NullLiteralExpression));
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

    public static void UnusedPrivate(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.MethodDeclaration))
        {
            if (LocalFacts.PrivateUnused(node, ctx.Model))
            {
                ctx.Report(id, node);
            }
        }
    }

    public static void FormatStringCall(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (Names.Invocation(node) == FormatMethodName && IsStringFormatCall(ctx, node))
            {
                ctx.Report(id, node);
            }
        }
    }

    public static void ConcatInLoop(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.AddAssignmentExpression))
        {
            if (node is AssignmentExpressionSyntax assignment
                && StmtFacts.InsideLoop(assignment)
                && IsStringTyped(ctx, assignment.Left))
            {
                ctx.Report(id, assignment);
            }
        }
    }

    public static void PreferNameof(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.StringLiteralExpression))
        {
            if (node is LiteralExpressionSyntax literal && MatchesEnclosingParameter(literal))
            {
                ctx.Report(id, node);
            }
        }
    }

    public static void ConsoleWriteLine(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (Names.Invocation(node) == WriteLineMethodName
                && Names.InvocationType(node) == ConsoleTypeName
                && !IsEntryPointOutput(node))
            {
                ctx.Report(id, node);
            }
        }
    }

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

    public static void DateTimeMissingKind(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.ObjectCreationExpression))
        {
            if (node is ObjectCreationExpressionSyntax creation
                && Names.Creation(creation) == DateTimeTypeName
                && !HasDateTimeKindArgument(creation))
            {
                ctx.Report(id, creation);
            }
        }
    }

    public static void OneStatement(WalkContext ctx, string id)
    {
        var semicolons = ctx.Tree.GetRoot().DescendantTokens()
            .Where(token => token.IsKind(SyntaxKind.SemicolonToken) && !IsExcludedSemicolon(token));
        foreach (var group in semicolons.GroupBy(token => token.GetLocation().GetLineSpan().StartLinePosition.Line))
        {
            if (group.Count() > 1)
            {
                ctx.Report(id, group.First().GetLocation());
            }
        }
    }

    private static bool IsExcludedSemicolon(SyntaxToken token) =>
        token.Parent is ForStatementSyntax or AccessorDeclarationSyntax;

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
            if (node is LiteralExpressionSyntax literal && HasRawControlCharacter(literal))
            {
                ctx.Report(id, node);
            }
        }
    }

    public static void BaseController(WalkContext ctx, string id) => BaseNamed(ctx, id, ControllerName);

    public static void ControllerMixedResponsibility(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (Names.Invocation(node) != ControllerName)
            {
                continue;
            }

            if (Shapes.Enclosing(node, SyntaxKind.ClassDeclaration) is ClassDeclarationSyntax type
                && !TypeFacts.EndsWith(type, ControllerName))
            {
                continue;
            }

            ctx.Report(id, node);
        }
    }

    private const string ModelStateName = "ModelState";

    private const string IsValidName = "IsValid";

    private const string ApiControllerAttributeName = "ApiController";

    public static void ModelStateChecked(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.MethodDeclaration))
        {
            if (node is not MethodDeclarationSyntax method
                || method.Body is null
                || !Shapes.HasModifier(method, SyntaxKind.PublicKeyword))
            {
                continue;
            }

            if (Shapes.Enclosing(method, SyntaxKind.ClassDeclaration) is not ClassDeclarationSyntax type
                || !TypeFacts.EndsWith(type, ControllerName)
                || HasAttribute(type, ApiControllerAttributeName))
            {
                continue;
            }

            var checksModelState = method.Body.DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>()
                .Any(member => member.Name.Identifier.Text == IsValidName
                    && Names.Simple(member.Expression) == ModelStateName);

            if (!checksModelState)
            {
                ctx.Report(id, method);
            }
        }
    }

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
            if (node is CatchClauseSyntax { Filter: not null })
            {
                continue;
            }

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

    private static IEnumerable<SyntaxNode> ExceptionNamedTypes(WalkContext ctx) =>
        ctx.Nodes(SyntaxKind.ClassDeclaration)
            .Concat(ctx.Nodes(SyntaxKind.RecordDeclaration))
            .Where(node => TypeFacts.EndsWith(node, ExceptionName));

    private static bool MissingStandardConstructors(SyntaxNode node)
    {
        var constructors = node.ChildNodes().OfType<ConstructorDeclarationSyntax>().ToList();
        var hasParameterless = constructors.Any(ctor => ctor.ParameterList.Parameters.Count == 0);
        var hasMessage = constructors.Any(ctor =>
            ctor.ParameterList.Parameters.Count == 1
            && IsStringParameter(ctor.ParameterList.Parameters[0]));
        var hasMessageAndInner = constructors.Any(ctor =>
            ctor.ParameterList.Parameters.Count == 2
            && IsStringParameter(ctor.ParameterList.Parameters[0])
            && IsExceptionParameter(ctor.ParameterList.Parameters[1]));

        return !(hasParameterless && hasMessage && hasMessageAndInner);
    }

    private const string StringTypeName = "string";

    private static bool IsStringParameter(ParameterSyntax parameter) =>
        parameter.Type is not null && Names.TypeText(parameter.Type) == StringTypeName;

    private static bool IsExceptionParameter(ParameterSyntax parameter) =>
        parameter.Type is not null
        && Names.TypeText(parameter.Type).EndsWith(ExceptionName, StringComparison.Ordinal);

    private static bool IsExceptionCreation(WalkContext ctx, SyntaxNode creation)
    {
        var type = Symbols.TypeOf(ctx, creation);
        if (type is not null && type.TypeKind != TypeKind.Error)
        {
            return Symbols.DerivesFrom(type, ExceptionName);
        }

        return creation is ObjectCreationExpressionSyntax typed
            && Names.TypeText(typed.Type).EndsWith(ExceptionName, StringComparison.Ordinal);
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

    private static bool IsEmptyType(SyntaxNode node) => Shapes.MemberCount(node) == 0;

    private static bool IsEmptyClass(SyntaxNode node) =>
        IsEmptyType(node) && !TypeFacts.BaseType(node).EndsWith(ExceptionName, StringComparison.Ordinal);

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

    private static bool IsStringFormatCall(WalkContext ctx, SyntaxNode node) =>
        Symbols.SymbolOf(ctx, node) is IMethodSymbol { ContainingType.SpecialType: SpecialType.System_String };

    private static bool IsStringTyped(WalkContext ctx, SyntaxNode node) =>
        Symbols.TypeOf(ctx, node)?.SpecialType == SpecialType.System_String;


    private static bool HasRawControlCharacter(LiteralExpressionSyntax literal)
    {
        foreach (var ch in literal.Token.Text)
        {
            if (ch is '\r' or '\n')
            {
                continue;
            }

            if (char.IsControl(ch))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEntryPointOutput(SyntaxNode node)
    {
        if (Shapes.Enclosing(node, SyntaxKind.GlobalStatement) is not null)
        {
            return true;
        }

        return Shapes.Enclosing(node, SyntaxKind.MethodDeclaration) is MethodDeclarationSyntax method
            && method.Identifier.Text == MainMethodName;
    }

    private static bool MatchesEnclosingParameter(LiteralExpressionSyntax literal)
    {
        if (Shapes.Enclosing(literal, SyntaxKind.MethodDeclaration) is not MethodDeclarationSyntax method)
        {
            return false;
        }

        var text = literal.Token.ValueText;
        foreach (var parameter in method.ParameterList.Parameters)
        {
            if (parameter.Identifier.Text == text)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUsing(SyntaxNode node)
    {
        if (Shapes.NestedIn(node, SyntaxKind.UsingStatement))
        {
            return true;
        }

        var local = Shapes.Enclosing(node, SyntaxKind.LocalDeclarationStatement);
        return local is LocalDeclarationStatementSyntax statement
            && statement.UsingKeyword.IsKind(SyntaxKind.UsingKeyword);
    }

    private static bool EscapesOwnership(SyntaxNode node)
    {
        if (node.Parent is ReturnStatementSyntax
            || node.Parent is ArrowExpressionClauseSyntax
            || node.Parent is SimpleLambdaExpressionSyntax
            || node.Parent is ParenthesizedLambdaExpressionSyntax)
        {
            return true;
        }

        if (node.Parent is ArgumentSyntax { Parent: ArgumentListSyntax { Parent: ObjectCreationExpressionSyntax } })
        {
            return true;
        }

        if (node.Parent is AssignmentExpressionSyntax assignment && assignment.Right == node)
        {
            return assignment.Left is MemberAccessExpressionSyntax
                || assignment.Parent is InitializerExpressionSyntax;
        }

        if (node.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator })
        {
            if (declarator.Parent?.Parent is FieldDeclarationSyntax)
            {
                return true;
            }

            return EscapesLater(declarator);
        }

        return false;
    }

    private static bool EscapesLater(VariableDeclaratorSyntax declarator)
    {
        var name = declarator.Identifier.Text;
        var scope = EnclosingFunctionBody(declarator);
        if (scope is null)
        {
            return false;
        }

        if (scope.DescendantNodes().OfType<ReturnStatementSyntax>()
            .Any(statement => statement.Expression is IdentifierNameSyntax identifier && identifier.Identifier.Text == name))
        {
            return true;
        }

        return scope.DescendantNodes().OfType<ArgumentSyntax>()
            .Any(argument => argument.Expression is IdentifierNameSyntax identifier
                && identifier.Identifier.Text == name
                && argument.Parent?.Parent is ObjectCreationExpressionSyntax);
    }

    private static SyntaxNode? EnclosingFunctionBody(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is MethodDeclarationSyntax or ConstructorDeclarationSyntax
                or LocalFunctionStatementSyntax or AccessorDeclarationSyntax)
            {
                return current;
            }
        }

        return null;
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

        if (Shapes.Enclosing(statement, SyntaxKind.MethodDeclaration) is MethodDeclarationSyntax method
            && (Shapes.HasModifier(method, SyntaxKind.PrivateKeyword) || Shapes.HasModifier(method, SyntaxKind.InternalKeyword)))
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
        if (!Shapes.HasModifier(node, SyntaxKind.ProtectedKeyword)
            || Shapes.HasModifier(node, SyntaxKind.OverrideKeyword))
        {
            return false;
        }

        var type = Shapes.Enclosing(node, SyntaxKind.ClassDeclaration);
        return type is not null && Shapes.HasModifier(type, SyntaxKind.SealedKeyword);
    }

    private const string DateTimeTypeName = "DateTime";

    private const string DateTimeKindTypeName = "DateTimeKind";

    private static bool HasDateTimeKindArgument(ObjectCreationExpressionSyntax creation) =>
        creation.ArgumentList?.Arguments.Any(argument =>
            argument.Expression is MemberAccessExpressionSyntax
            && Names.MemberType(argument.Expression) == DateTimeKindTypeName) ?? false;

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
        if (body is null)
        {
            return false;
        }

        var text = body.ToString();
        return !text.Contains(AssertName, StringComparison.Ordinal)
            && !text.Contains(VerifyMethodName, StringComparison.Ordinal);
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
            if (IsTestCaseAttribute(Names.Attribute(child)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTestCaseAttribute(string name) =>
        name is FactName or TheoryName or TestName or TestCaseName or TestCaseSourceName
            or TestMethodName or DataTestMethodName
        || name.EndsWith(FactName, StringComparison.Ordinal)
        || name.EndsWith(TheoryName, StringComparison.Ordinal);

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

        if (Shapes.ParameterCount(method) > 0)
        {
            return true;
        }

        var returnText = Names.TypeText(method.ReturnType);
        var isAsync = Shapes.HasModifier(method, SyntaxKind.AsyncKeyword);

        if (isAsync)
        {
            return returnText == VoidName;
        }

        return returnText != VoidName && returnText != TaskTypeName;
    }

    private static bool MissingAsyncSuffix(SyntaxNode node)
    {
        if (!Shapes.HasModifier(node, SyntaxKind.AsyncKeyword))
        {
            return false;
        }

        if (node is MethodDeclarationSyntax method)
        {
            if (method.Identifier.Text == MainMethodName)
            {
                return false;
            }

            if (HasAttribute(method, FactName) || HasAttribute(method, TestName) || HasAttribute(method, TheoryName))
            {
                return false;
            }
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

}

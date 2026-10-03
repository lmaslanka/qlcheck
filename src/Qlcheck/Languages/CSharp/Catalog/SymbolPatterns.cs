// Copyright (c) qlcheck contributors.
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class SymbolPatterns
{
    private const string MinMethodName = "Min";

    private const string MaxMethodName = "Max";

    private const string HashSetTypeName = "HashSet";

    private const string SortedSetTypeName = "SortedSet";

    private const string SetInterfaceName = "ISet";

    private const string ReadOnlySetInterfaceName = "IReadOnlySet";

    private static readonly HashSet<string> MinMaxMethods = new(StringComparer.Ordinal) { MinMethodName, MaxMethodName };

    private static readonly HashSet<string> SetTypeNames = new(StringComparer.Ordinal)
    {
        HashSetTypeName, SortedSetTypeName, SetInterfaceName, ReadOnlySetInterfaceName,
    };

    public static void SetMinMax(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation
                || invocation.Expression is not MemberAccessExpressionSyntax member
                || !MinMaxMethods.Contains(member.Name.Identifier.Text)
                || invocation.ArgumentList.Arguments.Count != 0)
            {
                continue;
            }

            if (IsSetType(Symbols.TypeOf(ctx, member.Expression)))
            {
                ctx.Report(id, invocation);
            }
        }
    }

    private static bool IsSetType(ITypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SetTypeNames.Contains(current.Name))
            {
                return true;
            }
        }

        return type is not null && type.AllInterfaces.Any(item => SetTypeNames.Contains(item.Name));
    }

    private const string JwtSecurityTokenHandlerTypeName = "JwtSecurityTokenHandler";

    private const string JwtSecurityTokenTypeName = "JwtSecurityToken";

    private const string SigningCredentialsTypeName = "SigningCredentials";

    private static readonly HashSet<string> JwtSigningTypeNames = new(StringComparer.Ordinal)
    {
        JwtSecurityTokenHandlerTypeName, JwtSecurityTokenTypeName, SigningCredentialsTypeName,
    };

    public static void JwtSigning(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation)
            {
                continue;
            }

            var symbol = Symbols.SymbolOf(ctx, invocation.Expression);
            if (symbol?.ContainingType is { } type && JwtSigningTypeNames.Contains(type.Name))
            {
                ctx.Report(id, invocation);
            }
        }

        foreach (var node in ctx.Nodes(SyntaxKind.ObjectCreationExpression))
        {
            if (node is ObjectCreationExpressionSyntax creation && JwtSigningTypeNames.Contains(Names.Creation(creation)))
            {
                ctx.Report(id, creation);
            }
        }
    }

    private static bool IsCallNamed(
        WalkContext ctx,
        InvocationExpressionSyntax invocation,
        string methodName,
        Func<INamedTypeSymbol, bool> containingTypeMatches)
    {
        if (Names.Invocation(invocation) != methodName)
        {
            return false;
        }

        var containingType = Symbols.SymbolOf(ctx, invocation.Expression)?.ContainingType;
        return containingType is not null && containingTypeMatches(containingType);
    }

    private static void ReportCallsNamed(
        WalkContext ctx,
        string id,
        string methodName,
        Func<INamedTypeSymbol, bool> containingTypeMatches)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is InvocationExpressionSyntax invocation && IsCallNamed(ctx, invocation, methodName, containingTypeMatches))
            {
                ctx.Report(id, invocation);
            }
        }
    }

    private const string ExitMethodName = "Exit";

    private const string EnvironmentTypeName = "Environment";

    public static void NoExit(WalkContext ctx, string id) =>
        ReportCallsNamed(ctx, id, ExitMethodName, type => type.Name == EnvironmentTypeName);

    private const string StartMethodName = "Start";

    private const string ProcessTypeName = "Process";

    public static void NoPathResolution(WalkContext ctx, string id) =>
        ReportCallsNamed(ctx, id, StartMethodName, type => type.Name == ProcessTypeName);

    private const string SuspendMethodName = "Suspend";

    private const string ThreadTypeName = "Thread";

    public static void NoThreadSuspend(WalkContext ctx, string id) =>
        ReportCallsNamed(ctx, id, SuspendMethodName, type => type.Name == ThreadTypeName);

    private const string LoadFromMethodName = "LoadFrom";

    private const string AssemblyTypeName = "Assembly";

    public static void PreferAssemblyLoad(WalkContext ctx, string id) =>
        ReportCallsNamed(ctx, id, LoadFromMethodName, type => type.Name == AssemblyTypeName);

    private const string LaunchMethodName = "Launch";

    private const string DebuggerTypeName = "Debugger";

    public static void NoDebugInProduction(WalkContext ctx, string id) =>
        ReportCallsNamed(ctx, id, LaunchMethodName, type => type.Name == DebuggerTypeName);

    private const string CreateClientMethodName = "CreateClient";

    private const string HttpClientFactoryInterfaceName = "IHttpClientFactory";

    public static void HttpClientFactoryCreate(WalkContext ctx, string id) =>
        ReportCallsNamed(ctx, id, CreateClientMethodName, type => Symbols.Implements(type, HttpClientFactoryInterfaceName));

    private const string OkMethodName = "Ok";

    private const string ControllerBaseTypeName = "ControllerBase";

    private const string ControllerTypeName = "Controller";

    public static void ProducesResponseType(WalkContext ctx, string id) =>
        ReportCallsNamed(ctx, id, OkMethodName, type => type.Name is ControllerBaseTypeName or ControllerTypeName);

    private const string NextMethodName = "Next";

    private const string RandomTypeName = "Random";

    public static void UnpredictableRandom(WalkContext ctx, string id) =>
        ReportCallsNamed(ctx, id, NextMethodName, type => Symbols.DerivesFrom(type, RandomTypeName));

    private const string TrueMethodName = "True";

    private const string AssertTypeName = "Assert";

    public static void NoBooleanLiteralAssert(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation
                || !IsCallNamed(ctx, invocation, TrueMethodName, type => type.Name == AssertTypeName)
                || invocation.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            var argument = invocation.ArgumentList.Arguments[0].Expression;
            if (argument.IsKind(SyntaxKind.TrueLiteralExpression) || argument.IsKind(SyntaxKind.FalseLiteralExpression))
            {
                ctx.Report(id, invocation);
            }
        }
    }

    private const string AreEqualMethodName = "AreEqual";

    private const int ExpectedArgumentCount = 2;

    public static void AssertArgumentOrder(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation
                || !IsCallNamed(ctx, invocation, AreEqualMethodName, type => type.Name == AssertTypeName)
                || invocation.ArgumentList.Arguments.Count < ExpectedArgumentCount)
            {
                continue;
            }

            var first = invocation.ArgumentList.Arguments[0].Expression;
            var second = invocation.ArgumentList.Arguments[1].Expression;
            if (first is not LiteralExpressionSyntax && second is LiteralExpressionSyntax)
            {
                ctx.Report(id, invocation);
            }
        }
    }

    private const string AssertMethodName = "Assert";

    private const string DebugTypeName = "Debug";

    private const string TraceTypeName = "Trace";

    public static void AssertNoSideEffect(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation
                || !IsCallNamed(ctx, invocation, AssertMethodName, type => type.Name is DebugTypeName or TraceTypeName)
                || invocation.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            if (HasSideEffect(invocation.ArgumentList.Arguments[0].Expression))
            {
                ctx.Report(id, invocation);
            }
        }
    }

    private static bool HasSideEffect(SyntaxNode node) =>
        node.DescendantNodesAndSelf().Any(descendant =>
            descendant is InvocationExpressionSyntax
                or AssignmentExpressionSyntax
                or PostfixUnaryExpressionSyntax
                or PrefixUnaryExpressionSyntax
                or ObjectCreationExpressionSyntax);

    private const int MessageOnlyArgumentCount = 1;

    public static void CompleteAssertion(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is InvocationExpressionSyntax invocation
                && IsCallNamed(ctx, invocation, AssertMethodName, type => type.Name is DebugTypeName or TraceTypeName)
                && invocation.ArgumentList.Arguments.Count == MessageOnlyArgumentCount)
            {
                ctx.Report(id, invocation);
            }
        }
    }


    private const string LoggerInterfaceName = "ILogger";

    private const string InformationMethodName = "Information";

    private const string LogMethodName = "Log";

    private const string LogErrorMethodName = "LogError";

    private const string LogCriticalMethodName = "LogCritical";

    private const string PlaceholderPatternText = @"\{[@$]?([A-Za-z_][A-Za-z0-9_]*|[0-9]+)(?:,-?[0-9]+)?(?::[^}]*)?\}";

    private static readonly Regex PlaceholderPattern = new(PlaceholderPatternText, RegexOptions.Compiled);

    private static bool IsLoggerCall(WalkContext ctx, InvocationExpressionSyntax invocation, string methodName) =>
        Names.Invocation(invocation) == methodName
        && invocation.Expression is MemberAccessExpressionSyntax member
        && Symbols.Implements(Symbols.TypeOf(ctx, member.Expression), LoggerInterfaceName);

    private static string? TemplateArgument(InvocationExpressionSyntax invocation)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return literal.Token.ValueText;
            }
        }

        return null;
    }

    private static List<string> ExtractPlaceholders(string template) =>
        PlaceholderPattern.Matches(template).Select(match => match.Groups[1].Value).ToList();

    public static void ConstantLogTemplate(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation
                || !IsLoggerCall(ctx, invocation, InformationMethodName)
                || invocation.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            if (invocation.ArgumentList.Arguments[0].Expression is not LiteralExpressionSyntax literal
                || !literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                ctx.Report(id, invocation);
            }
        }
    }

    public static void LogArgumentPosition(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation || !IsLoggerCall(ctx, invocation, InformationMethodName))
            {
                continue;
            }

            var template = TemplateArgument(invocation);
            if (template is null)
            {
                continue;
            }

            var placeholderCount = ExtractPlaceholders(template).Count;
            var argumentCount = invocation.ArgumentList.Arguments.Count - 1;
            if (placeholderCount != argumentCount)
            {
                ctx.Report(id, invocation);
            }
        }
    }

    public static void LogPlaceholderOrder(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation || !IsLoggerCall(ctx, invocation, InformationMethodName))
            {
                continue;
            }

            var template = TemplateArgument(invocation);
            if (template is null)
            {
                continue;
            }

            var numeric = ExtractPlaceholders(template)
                .Where(placeholder => int.TryParse(placeholder, out _))
                .Select(int.Parse)
                .ToList();
            if (numeric.Count > 1 && !IsAscending(numeric))
            {
                ctx.Report(id, invocation);
            }
        }
    }

    private static bool IsAscending(List<int> values)
    {
        for (var i = 1; i < values.Count; i++)
        {
            if (values[i] <= values[i - 1])
            {
                return false;
            }
        }

        return true;
    }

    public static void LogPlaceholderPascalCase(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation || !IsLoggerCall(ctx, invocation, InformationMethodName))
            {
                continue;
            }

            var template = TemplateArgument(invocation);
            if (template is null)
            {
                continue;
            }

            var hasBadCasing = ExtractPlaceholders(template)
                .Any(name => !int.TryParse(name, out _) && !TypeFacts.IsPascal(name));
            if (hasBadCasing)
            {
                ctx.Report(id, invocation);
            }
        }
    }

    public static void LogTemplateSyntax(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation || !IsLoggerCall(ctx, invocation, InformationMethodName))
            {
                continue;
            }

            var template = TemplateArgument(invocation);
            if (template is not null && !HasBalancedPlaceholders(template))
            {
                ctx.Report(id, invocation);
            }
        }
    }

    private static bool HasBalancedPlaceholders(string template)
    {
        var depth = 0;
        foreach (var ch in template)
        {
            if (ch == '{')
            {
                depth++;
                continue;
            }

            if (ch != '}')
            {
                continue;
            }

            depth--;
            if (depth < 0)
            {
                return false;
            }
        }

        return depth == 0;
    }

    public static void UniqueLogPlaceholder(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation || !IsLoggerCall(ctx, invocation, InformationMethodName))
            {
                continue;
            }

            var template = TemplateArgument(invocation);
            if (template is null)
            {
                continue;
            }

            var placeholders = ExtractPlaceholders(template);
            if (placeholders.Count != placeholders.Distinct(StringComparer.Ordinal).Count())
            {
                ctx.Report(id, invocation);
            }
        }
    }

    public static void LogCaughtException(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.CatchClause))
        {
            if (node is not CatchClauseSyntax catchClause
                || catchClause.Declaration is not { } declaration
                || string.IsNullOrEmpty(declaration.Identifier.Text)
                || catchClause.Block is null)
            {
                continue;
            }

            var exceptionName = declaration.Identifier.Text;
            foreach (var invocation in catchClause.Block.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!IsLoggerCall(ctx, invocation, LogMethodName))
                {
                    continue;
                }

                var referencesException = invocation.ArgumentList.Arguments.Any(argument =>
                    argument.Expression.DescendantNodesAndSelf()
                        .OfType<IdentifierNameSyntax>()
                        .Any(name => name.Identifier.Text == exceptionName));
                if (!referencesException)
                {
                    ctx.Report(id, invocation);
                }
            }
        }
    }

    public static void LogOrRethrow(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.CatchClause))
        {
            if (node is not CatchClauseSyntax catchClause || catchClause.Block is null)
            {
                continue;
            }

            var hasLog = catchClause.Block.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(invocation => IsLoggerCall(ctx, invocation, LogMethodName));
            var hasRethrow = catchClause.Block.DescendantNodes().OfType<ThrowStatementSyntax>().Any();
            if (hasLog && hasRethrow)
            {
                ctx.Report(id, catchClause);
            }
        }
    }

    public static void AzureFunctionLogsFailure(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.CatchClause))
        {
            if (node is not CatchClauseSyntax clause || !QuietPatterns.IsAzureFunctionMember(clause))
            {
                continue;
            }

            var logsFailure = clause.Block.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(invocation => IsLoggerCall(ctx, invocation, LogErrorMethodName)
                    || IsLoggerCall(ctx, invocation, LogCriticalMethodName));
            if (!logsFailure)
            {
                ctx.Report(id, clause);
            }
        }
    }

    private const string GetTypeMethodName = "GetType";

    public static void NoInterfaceCast(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation
                || invocation.Expression is not MemberAccessExpressionSyntax member
                || member.Name.Identifier.Text != GetTypeMethodName
                || invocation.ArgumentList.Arguments.Count != 0)
            {
                continue;
            }

            if (Symbols.TypeOf(ctx, member.Expression) is { TypeKind: TypeKind.Interface })
            {
                ctx.Report(id, invocation);
            }
        }
    }

    public static void SimplifyTypeCheck(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.EqualsExpression).Concat(ctx.Nodes(SyntaxKind.NotEqualsExpression)))
        {
            if (node is not BinaryExpressionSyntax comparison)
            {
                continue;
            }

            if (IsGetTypeCall(comparison.Left) && comparison.Right is TypeOfExpressionSyntax
                || IsGetTypeCall(comparison.Right) && comparison.Left is TypeOfExpressionSyntax)
            {
                ctx.Report(id, comparison);
            }
        }
    }

    private static bool IsGetTypeCall(ExpressionSyntax expression) =>
        expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } invocation
        && member.Name.Identifier.Text == GetTypeMethodName
        && invocation.ArgumentList.Arguments.Count == 0;

    private const string ReferenceEqualsMethodName = "ReferenceEquals";

    public static void NoReferenceEqualsValue(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation
                || Names.Invocation(invocation) != ReferenceEqualsMethodName
                || invocation.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            var hasValueTypeArgument = invocation.ArgumentList.Arguments
                .Any(argument => Symbols.TypeOf(ctx, argument.Expression) is { IsValueType: true });
            if (hasValueTypeArgument)
            {
                ctx.Report(id, invocation);
            }
        }
    }

    private const string HasValueMemberName = "HasValue";

    public static void RedundantNullableCompare(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.SimpleMemberAccessExpression))
        {
            if (node is not MemberAccessExpressionSyntax member || !IsNullableHasValue(ctx, member))
            {
                continue;
            }

            if (member.Parent is not BinaryExpressionSyntax comparison || !IsEqualityComparison(comparison))
            {
                continue;
            }

            var other = comparison.Left == member ? comparison.Right : comparison.Left;
            if (other.IsKind(SyntaxKind.TrueLiteralExpression) || other.IsKind(SyntaxKind.FalseLiteralExpression))
            {
                ctx.Report(id, comparison);
            }
        }
    }

    private static bool IsNullableHasValue(WalkContext ctx, MemberAccessExpressionSyntax member) =>
        member.Name.Identifier.Text == HasValueMemberName
        && Symbols.TypeOf(ctx, member.Expression)?.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    private static bool IsEqualityComparison(BinaryExpressionSyntax comparison) =>
        comparison.IsKind(SyntaxKind.EqualsExpression) || comparison.IsKind(SyntaxKind.NotEqualsExpression);

    private const string TypeTypeCheckName = "Type";

    private const string SystemNamespaceName = "System";

    public static void NoTypeOnType(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation
                || invocation.Expression is not MemberAccessExpressionSyntax member
                || member.Name.Identifier.Text != GetTypeMethodName)
            {
                continue;
            }

            var type = Symbols.TypeOf(ctx, member.Expression);
            if (type?.Name == TypeTypeCheckName && type.ContainingNamespace?.ToDisplayString() == SystemNamespaceName)
            {
                ctx.Report(id, invocation);
            }
        }
    }

    private const string ToLowerMethodName = "ToLower";

    public static void NormalizeUppercase(WalkContext ctx, string id) => ReportStringToLower(ctx, id);

    public static void StringCulture(WalkContext ctx, string id) => ReportStringToLower(ctx, id);

    private static void ReportStringToLower(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation
                || invocation.Expression is not MemberAccessExpressionSyntax member
                || member.Name.Identifier.Text != ToLowerMethodName)
            {
                continue;
            }

            if (Symbols.TypeOf(ctx, member.Expression)?.SpecialType == SpecialType.System_String)
            {
                ctx.Report(id, invocation);
            }
        }
    }

    private const string CastMethodName = "Cast";

    private const string EnumerableTypeName = "Enumerable";

    public static void NoExplicitForeachCast(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.ForEachStatement))
        {
            if (node is not ForEachStatementSyntax foreachStatement
                || foreachStatement.Expression is not InvocationExpressionSyntax invocation
                || Names.Invocation(invocation) != CastMethodName)
            {
                continue;
            }

            var symbol = Symbols.SymbolOf(ctx, invocation.Expression);
            if (symbol?.ContainingType?.Name == EnumerableTypeName)
            {
                ctx.Report(id, invocation);
            }
        }
    }

    private const string SumMethodName = "Sum";

    public static void SumOverflowCheck(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation || Names.Invocation(invocation) != SumMethodName)
            {
                continue;
            }

            var symbol = Symbols.SymbolOf(ctx, invocation.Expression);
            if (symbol?.ContainingType?.Name != EnumerableTypeName)
            {
                continue;
            }

            if (invocation.Ancestors().Any(ancestor => ancestor.IsKind(SyntaxKind.UncheckedStatement) || ancestor.IsKind(SyntaxKind.UncheckedExpression)))
            {
                ctx.Report(id, invocation);
            }
        }
    }

    private const string AsTaskMethodName = "AsTask";

    private const string ValueTaskTypeName = "ValueTask";

    public static void ConsumeValueTask(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation
                || invocation.Expression is not MemberAccessExpressionSyntax member
                || member.Name.Identifier.Text != AsTaskMethodName)
            {
                continue;
            }

            if (Symbols.TypeOf(ctx, member.Expression)?.Name == ValueTaskTypeName)
            {
                ctx.Report(id, invocation);
            }
        }
    }

    private const string GetResultMethodName = "GetResult";

    private const string TaskAwaiterTypeName = "TaskAwaiter";

    public static void NoBlockingAsync(WalkContext ctx, string id) =>
        ReportCallsNamed(ctx, id, GetResultMethodName, type => type.Name == TaskAwaiterTypeName);

    private const string WaitMethodName = "Wait";

    private const string TaskTypeName = "Task";

    public static void NoBlockingAsyncFunction(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation
                || invocation.Expression is not MemberAccessExpressionSyntax member
                || member.Name.Identifier.Text != WaitMethodName)
            {
                continue;
            }

            if (Symbols.DerivesFrom(Symbols.TypeOf(ctx, member.Expression), TaskTypeName))
            {
                ctx.Report(id, invocation);
            }
        }
    }

    private const string SendAsyncMethodName = "SendAsync";

    private const string HttpClientTypeName = "HttpClient";

    public static void ReuseClient(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is InvocationExpressionSyntax invocation
                && IsCallNamed(ctx, invocation, SendAsyncMethodName, type => type.Name == HttpClientTypeName)
                && QuietPatterns.IsAzureFunctionMember(invocation))
            {
                ctx.Report(id, invocation);
            }
        }
    }

    private const string GetFieldMethodName = "GetField";

    private const string NonPublicMemberName = "NonPublic";

    public static void NoReflectionAccessibility(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation
                || !IsCallNamed(ctx, invocation, GetFieldMethodName, type => type.Name == TypeTypeCheckName))
            {
                continue;
            }

            var referencesNonPublic = invocation.ArgumentList.Arguments.Any(argument =>
                argument.Expression.DescendantNodesAndSelf()
                    .OfType<MemberAccessExpressionSyntax>()
                    .Any(member => member.Name.Identifier.Text == NonPublicMemberName));
            if (referencesNonPublic)
            {
                ctx.Report(id, invocation);
            }
        }
    }

    private const string ReadMethodName = "Read";

    public static void CheckStreamRead(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation
                || invocation.Expression is not MemberAccessExpressionSyntax member
                || member.Name.Identifier.Text != ReadMethodName
                || !Symbols.DerivesFrom(Symbols.TypeOf(ctx, member.Expression), StreamTypeName))
            {
                continue;
            }

            if (invocation.Parent is ExpressionStatementSyntax)
            {
                ctx.Report(id, invocation);
            }
        }
    }

    private const string StreamTypeName = "Stream";

    private const string ValueMemberName = "Value";

    public static void NoEmptyNullableAccess(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.SimpleMemberAccessExpression))
        {
            if (node is not MemberAccessExpressionSyntax member || member.Name.Identifier.Text != ValueMemberName)
            {
                continue;
            }

            if (Symbols.TypeOf(ctx, member.Expression)?.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T)
            {
                continue;
            }

            if (!IsGuardedNullableAccess(member))
            {
                ctx.Report(id, member);
            }
        }
    }

    private static bool IsGuardedNullableAccess(MemberAccessExpressionSyntax member)
    {
        var baseText = member.Expression.ToString();
        SyntaxNode child = member;
        for (var parent = member.Parent; parent is not null; child = parent, parent = parent.Parent)
        {
            bool guarded;
            switch (parent)
            {
                case ConditionalExpressionSyntax conditional when child == conditional.WhenTrue:
                    guarded = IsNullGuard(conditional.Condition.ToString(), baseText);
                    break;
                case ConditionalExpressionSyntax conditional when child == conditional.WhenFalse:
                    guarded = IsNullDenial(conditional.Condition.ToString(), baseText);
                    break;
                case IfStatementSyntax ifStatement when child == ifStatement.Statement:
                    guarded = IsNullGuard(ifStatement.Condition.ToString(), baseText);
                    break;
                case IfStatementSyntax ifStatement when child == ifStatement.Else:
                    guarded = IsNullDenial(ifStatement.Condition.ToString(), baseText);
                    break;
                case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalAndExpression } andExpr when child == andExpr.Right:
                    guarded = IsNullGuard(andExpr.Left.ToString(), baseText);
                    break;
                case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalOrExpression } orExpr when child == orExpr.Right:
                    guarded = IsNullDenial(orExpr.Left.ToString(), baseText);
                    break;
                default:
                    guarded = false;
                    break;
            }

            if (guarded)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNullGuard(string condition, string baseText) =>
        condition.Contains($"{baseText}.HasValue", StringComparison.Ordinal)
        || condition.Contains($"{baseText} is not null", StringComparison.Ordinal)
        || condition.Contains($"{baseText} != null", StringComparison.Ordinal);

    private static bool IsNullDenial(string condition, string baseText) =>
        condition.Contains($"{baseText} == null", StringComparison.Ordinal)
        || condition.Contains($"{baseText} is null", StringComparison.Ordinal)
        || condition.Contains($"!{baseText}.HasValue", StringComparison.Ordinal);

    private const string ComputeHashMethodName = "ComputeHash";

    private const string HashAlgorithmTypeName = "HashAlgorithm";

    private static void ReportHashAlgorithmComputeHash(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation
                || invocation.Expression is not MemberAccessExpressionSyntax member
                || member.Name.Identifier.Text != ComputeHashMethodName)
            {
                continue;
            }

            if (Symbols.DerivesFrom(Symbols.TypeOf(ctx, member.Expression), HashAlgorithmTypeName))
            {
                ctx.Report(id, invocation);
            }
        }
    }

    public static void NoPlaintextPassword(WalkContext ctx, string id) => ReportHashAlgorithmComputeHash(ctx, id);

    public static void PasswordSalt(WalkContext ctx, string id) => ReportHashAlgorithmComputeHash(ctx, id);

    private const string BindMethodName = "Bind";

    private const string DirectoryEntryTypeName = "DirectoryEntry";

    private const string LdapConnectionTypeName = "LdapConnection";

    public static void LdapAuthenticated(WalkContext ctx, string id) =>
        ReportCallsNamed(ctx, id, BindMethodName, type => type.Name is DirectoryEntryTypeName or LdapConnectionTypeName);

    private const string ExportAttributeName = "Export";

    private const string PartCreationPolicyAttributeName = "PartCreationPolicy";

    private const string SharedPolicyName = "Shared";

    private static IEnumerable<AttributeSyntax> AttributesNamed(ClassDeclarationSyntax type, string name) =>
        type.AttributeLists.SelectMany(list => list.Attributes).Where(attribute => Names.Attribute(attribute) == name);

    public static void ExportImplementsContract(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.ClassDeclaration))
        {
            if (node is not ClassDeclarationSyntax type)
            {
                continue;
            }

            foreach (var attribute in AttributesNamed(type, ExportAttributeName))
            {
                var contractType = attribute.ArgumentList?.Arguments
                    .Select(argument => argument.Expression)
                    .OfType<TypeOfExpressionSyntax>()
                    .Select(typeOf => Names.TypeText(typeOf.Type))
                    .FirstOrDefault();
                if (contractType is null)
                {
                    continue;
                }

                var symbol = Symbols.Declared(ctx, type) as ITypeSymbol;
                if (!Symbols.Implements(symbol, contractType) && Names.Declared(type) != contractType)
                {
                    ctx.Report(id, type);
                }
            }
        }
    }

    public static void NoNewSharedPart(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.ObjectCreationExpression))
        {
            if (node is not ObjectCreationExpressionSyntax creation)
            {
                continue;
            }

            var declaration = (Symbols.TypeOf(ctx, creation.Type) as INamedTypeSymbol)?
                .DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax())
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault();
            if (declaration is null
                || !AttributesNamed(declaration, ExportAttributeName).Any()
                || !AttributesNamed(declaration, PartCreationPolicyAttributeName).Any(IsSharedPolicyAttribute))
            {
                continue;
            }

            ctx.Report(id, creation);
        }
    }

    private static bool IsSharedPolicyAttribute(AttributeSyntax attribute) =>
        attribute.ArgumentList?.Arguments.Any(argument =>
            argument.ToString().Contains(SharedPolicyName, StringComparison.Ordinal)) ?? false;

    public static void PartCreationPolicyNeedsExport(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.ClassDeclaration))
        {
            if (node is not ClassDeclarationSyntax type || !AttributesNamed(type, PartCreationPolicyAttributeName).Any())
            {
                continue;
            }

            if (!AttributesNamed(type, ExportAttributeName).Any())
            {
                ctx.Report(id, type);
            }
        }
    }
}

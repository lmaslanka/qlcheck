// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class SsrfPatterns
{
    private const string HttpClientTypeName = "HttpClient";

    private const string HttpRequestMessageTypeName = "HttpRequestMessage";

    private const string WebRequestTypeName = "WebRequest";

    private const string RequestUriPropertyName = "RequestUri";

    private const string CreateMethodName = "Create";

    private const string RequestUriParameterName = "requestUri";

    private const int MaxForwardingDepth = 2;

    private static readonly HashSet<string> HttpClientSinkMethods = new(StringComparer.Ordinal)
    {
        "GetAsync", "GetStringAsync", "GetByteArrayAsync", "GetStreamAsync",
        "PostAsync", "PutAsync", "PatchAsync", "DeleteAsync", "SendAsync",
    };

    private static readonly HashSet<string> EscapeMethodNames = new(StringComparer.Ordinal)
    {
        "EscapeDataString", "EscapeUriString", "UrlEncode",
    };

    private static readonly HashSet<string> SafeTypeNames = new(StringComparer.Ordinal)
    {
        "Guid", "DateTime", "DateTimeOffset",
    };

    public static void Ssrf(WalkContext ctx, string id)
    {
        foreach (var sink in UrlSinks(ctx))
        {
            if (sink.Url is not null && IsFullyTainted(ctx, sink.Url, 0))
            {
                ctx.Report(id, sink.Node);
            }
        }
    }

    public static void SsrfTraversal(WalkContext ctx, string id)
    {
        foreach (var sink in UrlSinks(ctx))
        {
            if (sink.Url is not null && HasUnsafePathSegment(ctx, sink.Url, 0))
            {
                ctx.Report(id, sink.Node);
            }
        }
    }

    private static IEnumerable<(SyntaxNode Node, ExpressionSyntax? Url)> UrlSinks(WalkContext ctx)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation
                || invocation.Expression is not MemberAccessExpressionSyntax member)
            {
                continue;
            }

            var methodName = member.Name.Identifier.Text;
            if (methodName == CreateMethodName && Names.MemberType(member) == WebRequestTypeName)
            {
                yield return (invocation, FirstArgument(invocation.ArgumentList));
                continue;
            }

            if (!HttpClientSinkMethods.Contains(methodName))
            {
                continue;
            }

            var receiverType = Symbols.TypeOf(ctx, member.Expression);
            if (receiverType is null || !Symbols.DerivesFrom(receiverType, HttpClientTypeName))
            {
                continue;
            }

            yield return (invocation, UrlArgument(invocation.ArgumentList, methodName));
        }

        foreach (var node in ctx.Nodes(SyntaxKind.ObjectCreationExpression))
        {
            if (node is ObjectCreationExpressionSyntax creation && Names.Creation(creation) == HttpRequestMessageTypeName)
            {
                yield return (creation, RequestUriArgument(creation.ArgumentList));
            }
        }

        foreach (var node in ctx.Nodes(SyntaxKind.SimpleAssignmentExpression))
        {
            if (node is not AssignmentExpressionSyntax assignment
                || assignment.Left is not MemberAccessExpressionSyntax target
                || target.Name.Identifier.Text != RequestUriPropertyName)
            {
                continue;
            }

            var receiverType = Symbols.TypeOf(ctx, target.Expression);
            if (receiverType is null || !Symbols.DerivesFrom(receiverType, HttpRequestMessageTypeName))
            {
                continue;
            }

            yield return (assignment, assignment.Right);
        }
    }

    private static ExpressionSyntax? UrlArgument(ArgumentListSyntax args, string methodName) =>
        methodName == "SendAsync" ? ResolveRequestUri(FirstArgument(args)) : FirstArgument(args);

    private static ExpressionSyntax? ResolveRequestUri(ExpressionSyntax? requestExpression)
    {
        if (requestExpression is ObjectCreationExpressionSyntax creation
            && Names.Creation(creation) == HttpRequestMessageTypeName)
        {
            return RequestUriArgument(creation.ArgumentList);
        }

        if (requestExpression is not IdentifierNameSyntax identifier)
        {
            return null;
        }

        var method = Shapes.Enclosing(identifier, SyntaxKind.MethodDeclaration);
        var declarator = method?.DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault(variable => variable.Identifier.Text == identifier.Identifier.Text);

        return declarator?.Initializer?.Value is ObjectCreationExpressionSyntax located
            && Names.Creation(located) == HttpRequestMessageTypeName
            ? RequestUriArgument(located.ArgumentList)
            : null;
    }

    private static ExpressionSyntax? RequestUriArgument(ArgumentListSyntax? args)
    {
        if (args is null)
        {
            return null;
        }

        var named = args.Arguments.FirstOrDefault(
            argument => argument.NameColon?.Name.Identifier.Text == RequestUriParameterName);
        if (named is not null)
        {
            return named.Expression;
        }

        return args.Arguments.Count >= 2 ? args.Arguments[1].Expression : null;
    }

    private static ExpressionSyntax? FirstArgument(ArgumentListSyntax args) =>
        args.Arguments.Count > 0 ? args.Arguments[0].Expression : null;

    private static bool IsFullyTainted(WalkContext ctx, ExpressionSyntax expression, int depth) =>
        Unwrap(expression) is IdentifierNameSyntax identifier && IsTaintedParameterReference(ctx, identifier, depth);

    private static bool HasUnsafePathSegment(WalkContext ctx, ExpressionSyntax expression, int depth)
    {
        var inner = Unwrap(expression);
        return inner switch
        {
            InterpolatedStringExpressionSyntax interpolated =>
                interpolated.Contents.OfType<InterpolationSyntax>().Any(hole => IsUnsafeValue(ctx, hole.Expression, depth)),
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression) =>
                IsUnsafeValue(ctx, binary.Left, depth) || IsUnsafeValue(ctx, binary.Right, depth),
            _ => false,
        };
    }

    private static bool IsUnsafeValue(WalkContext ctx, ExpressionSyntax expression, int depth)
    {
        var inner = Unwrap(expression);

        if (IsEscaped(inner) || IsSafeType(ctx, inner) || IsCompileTimeConstant(ctx, inner))
        {
            return false;
        }

        if (inner is InterpolatedStringExpressionSyntax
            || (inner is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression)))
        {
            return HasUnsafePathSegment(ctx, inner, depth);
        }

        return inner is IdentifierNameSyntax identifier && IsTaintedParameterReference(ctx, identifier, depth);
    }

    private static bool IsEscaped(ExpressionSyntax expression) =>
        expression is InvocationExpressionSyntax invocation && EscapeMethodNames.Contains(Names.Invocation(invocation));

    private static bool IsSafeType(WalkContext ctx, ExpressionSyntax expression)
    {
        var type = Symbols.TypeOf(ctx, expression);
        if (type is null)
        {
            return false;
        }

        return type.TypeKind == TypeKind.Enum || IsNumeric(type.SpecialType) || SafeTypeNames.Contains(type.Name);
    }

    private static bool IsNumeric(SpecialType type) =>
        type is SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16
            or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32
            or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single
            or SpecialType.System_Double or SpecialType.System_Decimal;

    private static bool IsCompileTimeConstant(WalkContext ctx, ExpressionSyntax expression) =>
        ctx.Model.GetConstantValue(expression).HasValue;

    private static bool IsTaintedParameterReference(WalkContext ctx, IdentifierNameSyntax identifier, int depth)
    {
        if (depth > MaxForwardingDepth
            || Shapes.Enclosing(identifier, SyntaxKind.MethodDeclaration) is not MethodDeclarationSyntax method)
        {
            return false;
        }

        var index = ParameterIndex(method, identifier.Identifier.Text);
        if (index < 0)
        {
            return false;
        }

        if (Shapes.HasModifier(method, SyntaxKind.PublicKeyword))
        {
            return true;
        }

        return CallSites(ctx, method)
            .Select(call => ArgumentExpressionAt(call, method, index))
            .Any(argument => argument is not null && IsUnsafeValue(ctx, argument, depth + 1));
    }

    private static int ParameterIndex(MethodDeclarationSyntax method, string name)
    {
        for (var i = 0; i < method.ParameterList.Parameters.Count; i++)
        {
            if (method.ParameterList.Parameters[i].Identifier.Text == name)
            {
                return i;
            }
        }

        return -1;
    }

    private static IEnumerable<InvocationExpressionSyntax> CallSites(WalkContext ctx, MethodDeclarationSyntax method)
    {
        var name = method.Identifier.Text;
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is InvocationExpressionSyntax invocation
                && !method.Contains(invocation)
                && Names.Simple(invocation.Expression) == name)
            {
                yield return invocation;
            }
        }
    }

    private static ExpressionSyntax? ArgumentExpressionAt(InvocationExpressionSyntax call, MethodDeclarationSyntax method, int index)
    {
        var args = call.ArgumentList.Arguments;
        var paramName = method.ParameterList.Parameters[index].Identifier.Text;

        var named = args.FirstOrDefault(argument => argument.NameColon?.Name.Identifier.Text == paramName);
        if (named is not null)
        {
            return named.Expression;
        }

        return index < args.Count ? args[index].Expression : null;
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression) =>
        expression is ParenthesizedExpressionSyntax paren ? Unwrap(paren.Expression) : expression;
}

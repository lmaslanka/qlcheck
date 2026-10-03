// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class ResourcePatterns
{
    private const string IncludeMethodName = "Include";

    private const string QueryableInterfaceName = "IQueryable";

    private static bool IsEfInclude(WalkContext ctx, InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax member
        && member.Name.Identifier.Text == IncludeMethodName
        && Symbols.Implements(Symbols.TypeOf(ctx, member.Expression), QueryableInterfaceName);

    private const int SingleInclude = 1;

    public static void CartesianExplosion(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation || !IsEfInclude(ctx, invocation))
            {
                continue;
            }

            var statement = invocation.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
            var includeCount = statement?.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Count(candidate => IsEfInclude(ctx, candidate)) ?? 0;
            if (includeCount > SingleInclude)
            {
                ctx.Report(id, invocation);
            }
        }
    }

    public static void DiscardedInclude(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is InvocationExpressionSyntax invocation
                && IsEfInclude(ctx, invocation)
                && invocation.Parent is ExpressionStatementSyntax)
            {
                ctx.Report(id, invocation);
            }
        }
    }

    public static void RedundantInclude(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation
                || !IsEfInclude(ctx, invocation)
                || invocation.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            var argumentText = invocation.ArgumentList.Arguments[0].ToString();
            var hasEarlierDuplicate = invocation.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(candidate => IsEfInclude(ctx, candidate)
                    && candidate.ArgumentList.Arguments.Count > 0
                    && candidate.ArgumentList.Arguments[0].ToString() == argumentText);
            if (hasEarlierDuplicate)
            {
                ctx.Report(id, invocation);
            }
        }
    }

    private const string EnterMethodName = "Enter";

    private const string MonitorTypeName = "Monitor";

    private static bool IsMonitorEnter(WalkContext ctx, InvocationExpressionSyntax invocation) =>
        Names.Invocation(invocation) == EnterMethodName
        && Symbols.SymbolOf(ctx, invocation.Expression)?.ContainingType?.Name == MonitorTypeName;

    public static void LockReadonlyField(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation
                || !IsMonitorEnter(ctx, invocation)
                || invocation.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            var argument = invocation.ArgumentList.Arguments[0].Expression;
            if (Symbols.SymbolOf(ctx, argument) is IFieldSymbol { IsReadOnly: false })
            {
                ctx.Report(id, invocation);
            }
        }
    }

    public static void WeakLock(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is InvocationExpressionSyntax invocation
                && IsMonitorEnter(ctx, invocation)
                && invocation.ArgumentList.Arguments.Count > 0
                && invocation.ArgumentList.Arguments[0].Expression.IsKind(SyntaxKind.ThisExpression))
            {
                ctx.Report(id, invocation);
            }
        }
    }

    private const string StringTypeName = "String";

    private const string TypeTypeName = "Type";

    private const string ThreadTypeName = "Thread";

    private const string MarshalByRefObjectTypeName = "MarshalByRefObject";

    private const string ExecutionContextTypeName = "ExecutionContext";

    private static readonly HashSet<string> WeakIdentityTypeNames = new(StringComparer.Ordinal)
    {
        StringTypeName, TypeTypeName, ThreadTypeName, MarshalByRefObjectTypeName, ExecutionContextTypeName,
    };

    public static void WeakLockObject(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.InvocationExpression))
        {
            if (node is not InvocationExpressionSyntax invocation
                || !IsMonitorEnter(ctx, invocation)
                || invocation.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            var type = Symbols.TypeOf(ctx, invocation.ArgumentList.Arguments[0].Expression);
            if (type is { TypeKind: TypeKind.Array } || (type is not null && WeakIdentityTypeNames.Contains(type.Name)))
            {
                ctx.Report(id, invocation);
            }
        }
    }

    private const string DisposeMethodName = "Dispose";

    private const string DisposableInterfaceName = "IDisposable";

    private static bool ImplementsDisposable(WalkContext ctx, SyntaxNode typeDeclaration) =>
        Symbols.Implements(Symbols.Declared(ctx, typeDeclaration) as ITypeSymbol, DisposableInterfaceName);

    public static void DisposeImplementsInterface(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.MethodDeclaration))
        {
            if (node is not MethodDeclarationSyntax method
                || method.Identifier.Text != DisposeMethodName
                || Shapes.Enclosing(method, SyntaxKind.ClassDeclaration) is not ClassDeclarationSyntax type)
            {
                continue;
            }

            if (!ImplementsDisposable(ctx, type))
            {
                ctx.Report(id, method);
            }
        }
    }

    private const string SuppressFinalizeMethodName = "SuppressFinalize";

    private static bool IsDisposeMethodOnDisposableType(WalkContext ctx, MethodDeclarationSyntax method) =>
        method.Identifier.Text == DisposeMethodName
        && method.Body is not null
        && Shapes.Enclosing(method, SyntaxKind.ClassDeclaration) is ClassDeclarationSyntax type
        && ImplementsDisposable(ctx, type);

    public static void DisposablePattern(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.MethodDeclaration))
        {
            if (node is not MethodDeclarationSyntax method || !IsDisposeMethodOnDisposableType(ctx, method))
            {
                continue;
            }

            var callsSuppressFinalize = method.Body!.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(invocation => Names.Invocation(invocation) == SuppressFinalizeMethodName);
            if (!callsSuppressFinalize)
            {
                ctx.Report(id, method);
            }
        }
    }

    private const string IntPtrTypeName = "IntPtr";

    private const string UIntPtrTypeName = "UIntPtr";

    private static bool HasUnmanagedHandleField(WalkContext ctx, ClassDeclarationSyntax type) =>
        type.Members.OfType<FieldDeclarationSyntax>().Any(field =>
        {
            var name = Names.TypeText(field.Declaration.Type);
            return name is IntPtrTypeName or UIntPtrTypeName;
        });

    public static void DisposableFinalizer(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.ClassDeclaration))
        {
            if (node is not ClassDeclarationSyntax type
                || !ImplementsDisposable(ctx, type)
                || !HasUnmanagedHandleField(ctx, type))
            {
                continue;
            }

            var hasFinalizer = type.Members.OfType<DestructorDeclarationSyntax>().Any();
            if (!hasFinalizer)
            {
                ctx.Report(id, type);
            }
        }
    }

    public static void DisposableMembers(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.ClassDeclaration))
        {
            if (node is not ClassDeclarationSyntax type || ImplementsDisposable(ctx, type))
            {
                continue;
            }

            if (OwnedDisposableFieldNames(ctx, type).Any())
            {
                ctx.Report(id, type);
            }
        }
    }

    public static void DisposeOwnMembers(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.ClassDeclaration))
        {
            if (node is not ClassDeclarationSyntax type)
            {
                continue;
            }

            var disposeMethod = type.Members
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(method => method.Identifier.Text == DisposeMethodName && method.Body is not null);
            if (disposeMethod is null)
            {
                continue;
            }

            var disposableFieldNames = OwnedDisposableFieldNames(ctx, type).ToList();
            if (disposableFieldNames.Count == 0)
            {
                continue;
            }

            var disposedNames = disposeMethod.Body!.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation => Names.Invocation(invocation) == DisposeMethodName)
                .Select(invocation => invocation.Expression)
                .OfType<MemberAccessExpressionSyntax>()
                .Select(member => Names.Simple(member.Expression))
                .ToHashSet(StringComparer.Ordinal);

            if (disposableFieldNames.Any(name => !disposedNames.Contains(name)))
            {
                ctx.Report(id, disposeMethod);
            }
        }
    }

    // A class is only responsible for disposing the disposable fields it *owns* -- the ones it
    // creates itself. A field assigned straight from a constructor parameter (dependency
    // injection) is owned by whoever constructed that dependency, not by this class.
    private static IEnumerable<string> OwnedDisposableFieldNames(WalkContext ctx, ClassDeclarationSyntax type)
    {
        var constructors = type.Members.OfType<ConstructorDeclarationSyntax>().ToList();
        foreach (var fieldDeclaration in type.Members.OfType<FieldDeclarationSyntax>())
        {
            if (!Symbols.Implements(Symbols.TypeOf(ctx, fieldDeclaration.Declaration.Type), DisposableInterfaceName))
            {
                continue;
            }

            foreach (var variable in fieldDeclaration.Declaration.Variables)
            {
                if (IsOwnedField(variable, constructors))
                {
                    yield return variable.Identifier.Text;
                }
            }
        }
    }

    private static bool IsOwnedField(VariableDeclaratorSyntax variable, IReadOnlyList<ConstructorDeclarationSyntax> constructors)
    {
        if (variable.Initializer is not null)
        {
            return variable.Initializer.Value is ObjectCreationExpressionSyntax;
        }

        var name = variable.Identifier.Text;
        foreach (var ctor in constructors)
        {
            if (ctor.Body is null)
            {
                continue;
            }

            foreach (var assignment in ctor.Body.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (!IsAssignmentToField(assignment, name))
                {
                    continue;
                }

                return assignment.Right is ObjectCreationExpressionSyntax;
            }
        }

        return true;
    }

    private static bool IsAssignmentToField(AssignmentExpressionSyntax assignment, string fieldName) =>
        assignment.Left switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text == fieldName,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } member => member.Name.Identifier.Text == fieldName,
            _ => false,
        };

    public static void NoDoubleDispose(WalkContext ctx, string id)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.Block))
        {
            if (node is not BlockSyntax block)
            {
                continue;
            }

            var disposeCallsByReceiver = block.Statements
                .SelectMany(statement => statement.DescendantNodesAndSelf())
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation => Names.Invocation(invocation) == DisposeMethodName
                    && invocation.Expression is MemberAccessExpressionSyntax member
                    && member.Expression is IdentifierNameSyntax
                    && Symbols.Implements(Symbols.TypeOf(ctx, member.Expression), DisposableInterfaceName))
                .GroupBy(invocation => Names.Simple(((MemberAccessExpressionSyntax)invocation.Expression).Expression));

            foreach (var group in disposeCallsByReceiver)
            {
                foreach (var extra in group.Skip(1))
                {
                    ctx.Report(id, extra);
                }
            }
        }
    }
}

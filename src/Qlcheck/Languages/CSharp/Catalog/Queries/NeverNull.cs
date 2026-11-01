// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Catalog.Queries;

// Never-null results (CONTEXT.md, plan 07). In order:
// 1. A callee annotated to return a non-nullable reference, with no [MaybeNull] or
//    [NotNullIfNotNull], is never null (an awaited Task<T> or ValueTask<T> by its T). A generic
//    return counts only where the caller's flow state says so, since the type argument decides.
// 2. A callee annotated `?` may return null, whatever its body does today (D1).
// 3. A callee without annotations that cannot be overridden (static, private, sealed, non-virtual
//    or a local function, D2) is read: an iterator never returns null, and a body whose every return
//    is new, empty, a literal, `this`, a never-null local or another never-null result (through `??`
//    and `?:`, at most three bodies deep) never returns null. Results are cached per callee.
internal sealed class NeverNull(SemanticModel model)
{
    private const int MaxBodies = 3;

    private const string EmptyMember = "Empty";

    private static readonly HashSet<string> TaskTypes = new(StringComparer.Ordinal) { "System.Threading.Tasks.Task`1", "System.Threading.Tasks.ValueTask`1" };

    private static readonly HashSet<string> EmptyFactories = new(StringComparer.Ordinal) { "System.Array", "System.Linq.Enumerable" };

    private static readonly HashSet<string> WeakeningAttributes = new(StringComparer.Ordinal) { "MaybeNullAttribute", "NotNullIfNotNullAttribute" };

    private readonly Dictionary<(IMethodSymbol Method, int Depth), NeverNullReason?> _bodies = [];

    private readonly HashSet<IMethodSymbol> _reading = new(SymbolEqualityComparer.Default);

    // Why the result of `invocation` (or of awaiting it, when it is awaited) cannot be null, or null
    // when it might be.
    public NeverNullReason? Reason(InvocationExpressionSyntax invocation) => Reason(invocation, model, 0);

    private NeverNullReason? Reason(InvocationExpressionSyntax invocation, SemanticModel at, int depth)
    {
        if (at.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method || Returned(method, invocation.Parent is AwaitExpressionSyntax) is not var (type, annotation, generic) || type.IsValueType)
        {
            return null;
        }

        return annotation switch
        {
            NullableAnnotation.Annotated => null,
            NullableAnnotation.NotAnnotated when generic => FlowSaysNotNull(invocation, at) ? new NeverNullReason(NeverNullKind.Declared, method, type) : null,
            NullableAnnotation.NotAnnotated => IsWeakened(method) ? null : new NeverNullReason(NeverNullKind.Declared, method, type),
            _ => Body(method.OriginalDefinition, type, depth),
        };
    }

    // The type a call gives (the T of an awaited Task<T>), its annotation, and whether the
    // declaration returns a type parameter. An awaited call that is not a Task<T> gives nothing.
    private static (ITypeSymbol Type, NullableAnnotation Annotation, bool Generic)? Returned(IMethodSymbol method, bool awaited)
    {
        if (!awaited)
        {
            return (method.ReturnType, method.ReturnNullableAnnotation, method.OriginalDefinition.ReturnType is ITypeParameterSymbol);
        }

        return method.ReturnType is INamedTypeSymbol { IsGenericType: true } task && TaskTypes.Contains(Symbols.QualifiedName(task))
            ? (task.TypeArguments[0], task.TypeArgumentNullableAnnotations[0], ((INamedTypeSymbol)method.OriginalDefinition.ReturnType).TypeArguments[0] is ITypeParameterSymbol)
            : null;
    }

    private static bool FlowSaysNotNull(InvocationExpressionSyntax invocation, SemanticModel at)
    {
        ExpressionSyntax value = invocation.Parent is AwaitExpressionSyntax awaited ? awaited : invocation;
        return at.GetNullableContext(invocation.SpanStart).AnnotationsEnabled() && at.GetTypeInfo(value).Nullability.FlowState == NullableFlowState.NotNull;
    }

    private static bool IsWeakened(IMethodSymbol method) =>
        method.GetReturnTypeAttributes().Any(attribute => attribute.AttributeClass?.Name is { } name && WeakeningAttributes.Contains(name));

    private static bool CanBeOverridden(IMethodSymbol method) =>
        method.MethodKind != MethodKind.LocalFunction
        && !method.IsStatic
        && method.DeclaredAccessibility != Accessibility.Private
        && !method.ContainingType.IsSealed
        && (method.IsVirtual || method.IsAbstract || method.IsOverride || method.ContainingType.TypeKind == TypeKind.Interface);

    // Reads an unannotated callee's body (rule 3).
    private NeverNullReason? Body(IMethodSymbol method, ITypeSymbol type, int depth)
    {
        if (depth >= MaxBodies || CanBeOverridden(method) || !_reading.Add(method))
        {
            return null;
        }

        try
        {
            if (!_bodies.TryGetValue((method, depth), out var reason))
            {
                reason = Read(method, type, depth);
                _bodies[(method, depth)] = reason;
            }

            return reason;
        }
        finally
        {
            _reading.Remove(method);
        }
    }

    private NeverNullReason? Read(IMethodSymbol method, ITypeSymbol type, int depth)
    {
        var declaration = method.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()).FirstOrDefault(node => node is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax);
        if (declaration is null)
        {
            return null;
        }

        var at = model.Compilation.GetSemanticModel(declaration.SyntaxTree);
        var own = OwnNodes(declaration).ToList();
        if (own.OfType<YieldStatementSyntax>().Any())
        {
            return new NeverNullReason(NeverNullKind.Iterator, method, type);
        }

        var returns = Arrow(declaration) is { } arrow ? [arrow] : own.OfType<ReturnStatementSyntax>().Select(statement => statement.Expression).ToList();
        return returns.Count > 0 && returns.All(value => value is not null && IsNeverNull(value, at, declaration, depth + 1))
            ? new NeverNullReason(NeverNullKind.ReturnsNewOrEmpty, method, type)
            : null;
    }

    private static ExpressionSyntax? Arrow(SyntaxNode declaration) =>
        declaration switch
        {
            BaseMethodDeclarationSyntax { ExpressionBody: { } body } => body.Expression,
            LocalFunctionStatementSyntax { ExpressionBody: { } body } => body.Expression,
            _ => null,
        };

    // The nodes of a member's body, not those of lambdas or local functions declared in it.
    private static IEnumerable<SyntaxNode> OwnNodes(SyntaxNode declaration) =>
        declaration.DescendantNodes(node => node == declaration || node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax));

    private bool IsNeverNull(ExpressionSyntax value, SemanticModel at, SyntaxNode body, int depth) =>
        value switch
        {
            ParenthesizedExpressionSyntax parenthesized => IsNeverNull(parenthesized.Expression, at, body, depth),
            CastExpressionSyntax cast => IsNeverNull(cast.Expression, at, body, depth),
            BaseObjectCreationExpressionSyntax or AnonymousObjectCreationExpressionSyntax or ArrayCreationExpressionSyntax
                or ImplicitArrayCreationExpressionSyntax or CollectionExpressionSyntax or InterpolatedStringExpressionSyntax
                or ThisExpressionSyntax or ThrowExpressionSyntax => true,
            LiteralExpressionSyntax literal => literal.Kind() is not (SyntaxKind.NullLiteralExpression or SyntaxKind.DefaultLiteralExpression),
            BinaryExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceExpression } coalesce => IsNeverNull(coalesce.Right, at, body, depth),
            ConditionalExpressionSyntax conditional => IsNeverNull(conditional.WhenTrue, at, body, depth) && IsNeverNull(conditional.WhenFalse, at, body, depth),
            AwaitExpressionSyntax { Expression: InvocationExpressionSyntax awaited } => Reason(awaited, at, depth) is not null,
            InvocationExpressionSyntax invocation => IsEmptyFactory(invocation, at) || Reason(invocation, at, depth) is not null,
            MemberAccessExpressionSyntax => at.GetSymbolInfo(value).Symbol is IFieldSymbol { Name: EmptyMember, ContainingType.SpecialType: SpecialType.System_String },
            IdentifierNameSyntax => at.GetSymbolInfo(value).Symbol is ILocalSymbol local && IsNeverNullLocal(local, at, body, depth),
            _ => false,
        };

    // `Array.Empty<T>()` and `Enumerable.Empty<T>()`.
    private static bool IsEmptyFactory(InvocationExpressionSyntax invocation, SemanticModel at) =>
        at.GetSymbolInfo(invocation).Symbol is IMethodSymbol { Name: EmptyMember } method && EmptyFactories.Contains(Symbols.QualifiedName(method.ContainingType));

    // A local whose every assignment in the body is never null, and which is never passed by ref
    // or out.
    private bool IsNeverNullLocal(ILocalSymbol local, SemanticModel at, SyntaxNode body, int depth)
    {
        bool IsLocal(SyntaxNode node) => SymbolEqualityComparer.Default.Equals(at.GetSymbolInfo(node).Symbol, local);
        var own = OwnNodes(body).ToList();
        if (own.OfType<ArgumentSyntax>().Any(argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None) && IsLocal(argument.Expression)))
        {
            return false;
        }

        var values = own.OfType<VariableDeclaratorSyntax>()
            .Where(declarator => SymbolEqualityComparer.Default.Equals(at.GetDeclaredSymbol(declarator), local))
            .Select(declarator => declarator.Initializer?.Value)
            .Concat(own.OfType<AssignmentExpressionSyntax>().Where(assignment => IsLocal(assignment.Left)).Select(assignment => (ExpressionSyntax?)assignment.Right))
            .ToList();
        return values.Count > 0 && values.All(value => value is not null && IsNeverNull(value, at, body, depth));
    }
}

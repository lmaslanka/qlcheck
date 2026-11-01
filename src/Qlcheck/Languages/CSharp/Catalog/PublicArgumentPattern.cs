// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog;

// validate-public-arguments: a reference-type parameter of a member visible outside the assembly
// that is dereferenced before any null check, in code where nullable annotations are off. In
// annotated code the compiler's own warnings (NullablePattern) cover this instead. "Before" is
// source order within the member, so a check in an earlier sibling branch also counts: the rule errs
// toward silence. A file with compile errors gives nothing (ADR 0003).
internal static class PublicArgumentPattern
{
    private const string ReferenceEqualsMethod = "ReferenceEquals";

    private const string ArgumentNullExceptionType = "ArgumentNullException";

    private static readonly HashSet<string> NotNullAttributes = new(StringComparer.Ordinal)
    {
        "NotNullAttribute", "ValidatedNotNullAttribute",
    };

    public static void Apply(WalkContext ctx, string id)
    {
        var hasErrors = new Lazy<bool>(() => ctx.Model.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        foreach (var syntax in ctx.Tree.GetRoot().DescendantNodes().OfType<ParameterSyntax>())
        {
            if (Candidate(ctx, syntax) is not var (parameter, member) || hasErrors.Value)
            {
                continue;
            }

            var references = member.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Where(name => name.Identifier.ValueText == parameter.Name
                    && SymbolEqualityComparer.Default.Equals(ctx.Model.GetSymbolInfo(name).Symbol, parameter))
                .ToList();
            var dereference = references.FirstOrDefault(name => IsDereference(ctx, name));
            if (dereference is not null && !references.Any(name => name.SpanStart < dereference.SpanStart && IsValidation(ctx, name)))
            {
                ctx.Report(id, dereference);
            }
        }
    }

    // The parameter and its member, when the parameter is in scope: a reference type, not out or
    // params, not defaulted to null, declared where annotations are off, on a member visible
    // outside the assembly that does not inherit its contract and is not a test.
    private static (IParameterSymbol Parameter, SyntaxNode Member)? Candidate(WalkContext ctx, ParameterSyntax syntax)
    {
        var member = syntax.Parent?.Parent;
        if (member is not (BaseMethodDeclarationSyntax or IndexerDeclarationSyntax)
            || ctx.Model.GetDeclaredSymbol(syntax) is not { } parameter)
        {
            return null;
        }

        if (!IsRequiredReference(parameter) || ctx.Model.GetNullableContext(syntax.SpanStart).AnnotationsEnabled())
        {
            return null;
        }

        return IsInScope(parameter.ContainingSymbol, member) ? (parameter, member) : null;
    }

    // A reference the caller must pass: not out, not params, and with no default (`= null` is a
    // deliberately optional argument).
    private static bool IsRequiredReference(IParameterSymbol parameter) =>
        parameter.Type.IsReferenceType
        && parameter.RefKind != RefKind.Out
        && !parameter.IsParams
        && !parameter.HasExplicitDefaultValue;

    private static bool IsInScope(ISymbol member, SyntaxNode syntax)
    {
        if (InheritsContract(member))
        {
            return false;
        }

        var attributes = syntax is MemberDeclarationSyntax declaration ? declaration.AttributeLists.SelectMany(list => list.Attributes) : [];
        if (attributes.Any(attribute => TestMethods.AttributeNames.Contains(Names.Simple(attribute.Name))))
        {
            return false;
        }

        for (ISymbol? symbol = member; symbol is not null and not INamespaceSymbol; symbol = symbol.ContainingSymbol)
        {
            if (symbol.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
            {
                return false;
            }
        }

        return true;
    }

    // An override or explicit interface implementation: the base declares the parameter's contract.
    private static bool InheritsContract(ISymbol member) =>
        member switch
        {
            IMethodSymbol method => method.IsOverride || method.ExplicitInterfaceImplementations.Length > 0,
            IPropertySymbol property => property.IsOverride || property.ExplicitInterfaceImplementations.Length > 0,
            _ => false,
        };

    // Receiver of a member access (not an extension call, which may accept null), an element
    // access, a foreach or a lock. `?.`, `??`, arguments and nameof are not dereferences.
    private static bool IsDereference(WalkContext ctx, IdentifierNameSyntax name) =>
        name.Parent switch
        {
            MemberAccessExpressionSyntax member when member.Expression == name =>
                ctx.Model.GetSymbolInfo(member).Symbol is not IMethodSymbol { IsExtensionMethod: true },
            ElementAccessExpressionSyntax element => element.Expression == name,
            ForEachStatementSyntax loop => loop.Expression == name,
            LockStatementSyntax gate => gate.Expression == name,
            _ => false,
        };

    // A null check of the parameter: a comparison with null, an `is` pattern, ReferenceEquals,
    // `?? throw`, `throw new ArgumentNullException(nameof(p))`, or passing it to a parameter marked
    // [NotNull]/[ValidatedNotNull] such as ArgumentNullException.ThrowIfNull's.
    private static bool IsValidation(WalkContext ctx, IdentifierNameSyntax name) =>
        name.Parent switch
        {
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression) =>
                IsNull(binary.Left) || IsNull(binary.Right),
            BinaryExpressionSyntax coalesce when coalesce.IsKind(SyntaxKind.CoalesceExpression) =>
                coalesce.Left == name && coalesce.Right is ThrowExpressionSyntax,
            IsPatternExpressionSyntax pattern => pattern.Expression == name,
            ArgumentSyntax argument => IsCheckingArgument(ctx, argument),
            _ => false,
        };

    private static bool IsCheckingArgument(WalkContext ctx, ArgumentSyntax argument)
    {
        if (argument.Parent is not ArgumentListSyntax { Parent: { } call } list)
        {
            return false;
        }

        if (call is InvocationExpressionSyntax invocation && Names.Invocation(invocation) == ReferenceEqualsMethod)
        {
            return list.Arguments.Any(other => IsNull(other.Expression));
        }

        if (call is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } }
            && call.Parent?.Parent?.Parent is ObjectCreationExpressionSyntax creation
            && Names.Simple(creation.Type) == ArgumentNullExceptionType)
        {
            return true;
        }

        return LiteralContext.SymbolOf(ctx.Model, call) is IMethodSymbol method
            && LiteralContext.ParameterOf(method, list, argument) is { } target
            && target.GetAttributes().Any(attribute => attribute.AttributeClass?.Name is { } attributeName && NotNullAttributes.Contains(attributeName));
    }

    private static bool IsNull(ExpressionSyntax expression) => expression.IsKind(SyntaxKind.NullLiteralExpression);
}

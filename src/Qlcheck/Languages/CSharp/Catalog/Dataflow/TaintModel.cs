// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// What the analysis knows about the world: which values are untrusted on entry (D1), which types
// cannot carry an attack, and which calls return untrusted data.
internal static class TaintModel
{
    private const string ConsoleType = "System.Console";

    private static readonly HashSet<string> ConsoleReads = new(StringComparer.Ordinal) { "ReadLine", "ReadLineAsync" };

    private static readonly HashSet<string> SafeTypes = new(StringComparer.Ordinal)
    {
        "System.Guid", "System.DateTime", "System.DateTimeOffset", "System.DateOnly", "System.TimeOnly", "System.TimeSpan",
        "System.Threading.CancellationToken",
    };

    // The entry state: every parameter of a method declared public, except those of a safe type.
    // A numeric parameter is Numeric, which only the allocation and loop-bound rows accept.
    public static TaintState Seed(IMethodSymbol? owner)
    {
        var state = new TaintState();
        if (owner is null || owner.DeclaredAccessibility != Accessibility.Public)
        {
            return state;
        }

        foreach (var parameter in owner.Parameters)
        {
            var label = LabelFor(parameter.Type);
            if (label != TaintLabel.None)
            {
                state.Set(parameter, Taint.From(label, new TaintSource(parameter.Name, $"parameter of public `{MemberName(owner)}`", Line(parameter))));
            }
        }

        return state;
    }

    // The source a call stands for when it returns untrusted data, or null for any other call.
    public static TaintSource? SourceOf(IMethodSymbol method, SyntaxNode at) =>
        ConsoleReads.Contains(method.Name) && Symbols.QualifiedName(method.ContainingType) == ConsoleType
            ? new TaintSource($"Console.{method.Name}()", "console input", at.GetLocation().GetLineSpan().StartLinePosition.Line + 1)
            : null;

    // A value of this type cannot carry an injection: numbers, bool, enums, GUIDs, dates, time
    // spans, cancellation tokens and nullables of those. A char can still be a quote.
    public static bool IsSafe(ITypeSymbol? type) =>
        type is not null
        && (type.SpecialType is >= SpecialType.System_Boolean and <= SpecialType.System_Double and not SpecialType.System_Char
            || type.TypeKind == TypeKind.Enum
            || SafeTypes.Contains(Symbols.QualifiedName(type))
            || Underlying(type) is { } inner && IsSafe(inner));

    public static bool IsNumeric(ITypeSymbol? type) =>
        (Underlying(type) ?? type)?.SpecialType is >= SpecialType.System_SByte and <= SpecialType.System_Double;

    // The label a value of this type starts with when it comes from an untrusted source.
    public static TaintLabel LabelFor(ITypeSymbol? type)
    {
        if (IsNumeric(type))
        {
            return TaintLabel.Numeric;
        }

        return IsSafe(type) ? TaintLabel.None : TaintLabel.Untrusted;
    }

    private static ITypeSymbol? Underlying(ITypeSymbol? type) =>
        type is INamedTypeSymbol { IsGenericType: true } named && named.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T
            ? named.TypeArguments[0]
            : null;

    private static string MemberName(IMethodSymbol method) =>
        method switch
        {
            { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } => method.ContainingType.Name,
            { AssociatedSymbol: { } property } => property.Name,
            _ => method.Name,
        };

    private static int Line(ISymbol symbol) =>
        symbol.Locations.FirstOrDefault(location => location.IsInSource) is { } location
            ? location.GetLineSpan().StartLinePosition.Line + 1
            : 0;
}

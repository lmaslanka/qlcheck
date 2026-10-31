// Copyright (c) qlcheck contributors.
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// What the analysis knows about the world: which values are untrusted on entry (D1), which types
// cannot carry an attack, and which calls return untrusted data.
internal static class TaintModel
{
    private const string ConsoleType = "System.Console";

    private const string StartInfoType = "System.Diagnostics.ProcessStartInfo";

    private const string ProgramParameter = "fileName";

    private const string ProgramProperty = "FileName";

    private static readonly HashSet<string> Shells = new(StringComparer.OrdinalIgnoreCase)
    {
        "sh", "bash", "zsh", "dash", "cmd", "powershell", "pwsh",
    };

    private const string ConsoleInput = "In";

    private const string BuilderType = "System.Text.StringBuilder";

    private static readonly HashSet<string> TaskTypes = new(StringComparer.Ordinal)
    {
        "System.Threading.Tasks.Task", "System.Threading.Tasks.Task`1", "System.Threading.Tasks.ValueTask", "System.Threading.Tasks.ValueTask`1",
    };

    private static readonly HashSet<string> TaskPropagators = new(StringComparer.Ordinal) { "FromResult", "ConfigureAwait" };

    private const string PathType = "System.IO.Path";

    private static readonly HashSet<string> PathPropagators = new(StringComparer.Ordinal)
    {
        "Combine", "Join", "GetFullPath", "GetDirectoryName", "ChangeExtension", "GetRelativePath",
    };

    private static readonly HashSet<string> BuilderMutators = new(StringComparer.Ordinal)
    {
        "Append", "AppendLine", "AppendFormat", "AppendJoin", "Insert", "Replace",
    };

    private const string ConvertType = "System.Convert";

    private const string MathType = "System.Math";

    private const string EnumType = "System.Enum";

    private const string FullPathMethod = "GetFullPath";

    private static readonly HashSet<string> ParseMethods = new(StringComparer.Ordinal) { "Parse", "TryParse", "ParseExact", "TryParseExact" };

    private static readonly HashSet<string> FileNameMethods = new(StringComparer.Ordinal) { "GetFileName", "GetFileNameWithoutExtension" };

    private const string EnumerableType = "System.Collections.IEnumerable";

    private const string EnumeratorMethod = "GetEnumerator";

    private static readonly HashSet<string> CollectionMutators = new(StringComparer.Ordinal)
    {
        "Add", "AddRange", "Insert", "InsertRange", "Push", "Enqueue", "TryAdd",
    };

    private static readonly HashSet<string> LinqTypes = new(StringComparer.Ordinal) { "System.Linq.Enumerable", "System.Linq.Queryable" };

    // Constructing one of these from tainted arguments gives a tainted object.
    private static readonly HashSet<string> CarrierTypes = new(StringComparer.Ordinal)
    {
        "System.String", BuilderType, "System.Uri",
    };

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

    // The source a call stands for when it returns untrusted data, or null for any other call:
    // `Console.ReadLine()` and `Console.In.ReadLine()`, and their async forms.
    public static TaintSource? SourceOf(IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;
        if (!ConsoleReads.Contains(method.Name))
        {
            return null;
        }

        var name = invocation.Instance switch
        {
            null when Symbols.QualifiedName(method.ContainingType) == ConsoleType => $"Console.{method.Name}()",
            IPropertyReferenceOperation { Property.Name: ConsoleInput } input when Symbols.QualifiedName(input.Property.ContainingType) == ConsoleType =>
                $"Console.{ConsoleInput}.{method.Name}()",
            _ => null,
        };
        return name is null ? null : new TaintSource(name, "console input", invocation.Syntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
    }

    // Calls whose result carries the taint of their receiver and arguments (D5): string methods,
    // StringBuilder, the path-building Path methods, ToString, GetEnumerator (how foreach reads a
    // collection), and Task.FromResult and ConfigureAwait. Any other call returns clean data.
    public static bool Propagates(IMethodSymbol method) =>
        method.Name is nameof(ToString) or EnumeratorMethod
        || method.ContainingType.SpecialType == SpecialType.System_String
        || Symbols.QualifiedName(method.ContainingType) == BuilderType
        || Symbols.QualifiedName(method.ContainingType) == PathType && PathPropagators.Contains(method.Name)
        || TaskTypes.Contains(Symbols.QualifiedName(method.ContainingType)) && TaskPropagators.Contains(method.Name);

    // A sanitizer (D7) keeps the source of its input but changes the labels: parsing or converting
    // to a number leaves Numeric, to another safe type nothing; Path.GetFileName leaves FileName,
    // which the path rows do not accept; Math.Min or Math.Clamp with a constant upper bound clears
    // Numeric. Null when the call is not a sanitizer.
    public static Taint? Sanitize(IInvocationOperation invocation, Taint input)
    {
        var method = invocation.TargetMethod;
        var type = Symbols.QualifiedName(method.ContainingType);
        if (IsParse(method) || type == ConvertType)
        {
            return Retype(invocation.Type, input);
        }

        if (type == PathType && FileNameMethods.Contains(method.Name))
        {
            return input.Relabel(input.IsNone ? TaintLabel.None : TaintLabel.FileName);
        }

        if (type != MathType)
        {
            return null;
        }

        return HasConstantBound(invocation) ? Taint.None : Retype(invocation.Type, input);
    }

    // `int.Parse`, `Guid.TryParse`, `Enum.Parse<T>` and their Exact forms.
    public static bool IsParse(IMethodSymbol method) =>
        ParseMethods.Contains(method.Name) && (IsSafe(method.ContainingType) || Symbols.QualifiedName(method.ContainingType) == EnumType);

    // `Path.GetFullPath`, whose result a StartsWith check can prove contained (TaintGuards).
    public static bool IsFullPath(IOperation operation) =>
        operation is IInvocationOperation { TargetMethod: { Name: FullPathMethod } method } && Symbols.QualifiedName(method.ContainingType) == PathType;

    // A value of this type keeps its taint, except that a number keeps only Numeric and another
    // safe type keeps nothing.
    public static Taint Retype(ITypeSymbol? type, Taint taint)
    {
        if (IsNumeric(type))
        {
            return taint.Relabel(taint.IsNone ? TaintLabel.None : TaintLabel.Numeric);
        }

        return IsSafe(type) ? Taint.None : taint;
    }

    private static bool HasConstantBound(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name switch
        {
            nameof(Math.Min) => invocation.Arguments.Any(argument => argument.Value.ConstantValue.HasValue),
            nameof(Math.Clamp) => invocation.Arguments[^1].Value.ConstantValue.HasValue,
            _ => false,
        };

    // Calls that put their arguments' taint into the receiver: `builder.Append(x)`, `list.Add(x)`.
    public static bool MutatesReceiver(IMethodSymbol method) =>
        BuilderMutators.Contains(method.Name) && Symbols.QualifiedName(method.ContainingType) == BuilderType
        || CollectionMutators.Contains(method.Name) && IsCollection(method.ContainingType);

    // A LINQ operator: its result carries the taint of its source sequence, the first argument.
    public static bool IsLinq(IMethodSymbol method) => LinqTypes.Contains(Symbols.QualifiedName(method.ContainingType));

    // A collection holds the taint of what was put in it; reading an element, by indexer, foreach
    // (Current) or LINQ, gives it back. A string is enumerable but immutable, so it is not one.
    public static bool IsCollection(ITypeSymbol? type) =>
        type is not null && type.SpecialType != SpecialType.System_String && Symbols.Is(type, EnumerableType);

    public static bool CarriesTaint(ITypeSymbol? type) => type is not null && CarrierTypes.Contains(Symbols.QualifiedName(type));

    // A constant naming a command shell, with or without a directory or `.exe`.
    public static bool IsShell(Optional<object?> constant) =>
        constant is { HasValue: true, Value: string program } && Shells.Contains(Path.GetFileNameWithoutExtension(program));

    // A call or constructor whose `fileName` argument is a constant shell.
    public static bool StartsShell(ImmutableArray<IArgumentOperation> arguments) =>
        arguments.Any(argument => argument.Parameter?.Name == ProgramParameter && IsShell(argument.Value.ConstantValue));

    // `ProcessStartInfo.FileName`, the program a process runs.
    public static bool IsProgram(IPropertySymbol property) =>
        property.Name == ProgramProperty && Symbols.Is(property.ContainingType, StartInfoType);

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

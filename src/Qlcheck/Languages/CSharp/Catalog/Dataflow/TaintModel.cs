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

    private const string AttributeSuffix = "Attribute";

    private const string RangeAttribute = "Range";

    private const string NonActionAttribute = "NonAction";

    private const string ApiControllerAttribute = "ApiController";

    private const string ControllerBaseType = "ControllerBase";

    private const string RequestType = "HttpRequest";

    private static readonly string[] BindingAttributes = ["FromQuery", "FromRoute", "FromForm", "FromBody", "FromHeader"];

    private static readonly HashSet<string> RequestParts = new(StringComparer.Ordinal)
    {
        "Query", "Form", "Headers", "Cookies", "RouteValues", "Path", "QueryString", "Body",
    };

    private const string ExceptionType = "System.Exception";

    private const string ExceptionDetail = "exception detail";

    private const string StackTraceProperty = "StackTrace";

    private const string ZipEntryType = "System.IO.Compression.ZipArchiveEntry";

    private const string TarEntryType = "System.Formats.Tar.TarEntry";

    private const string EntryFullNameProperty = "FullName";

    private const string EntryNameProperty = "Name";

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

    private const string RegexType = "System.Text.RegularExpressions.Regex";

    private const string EscapeMethod = "Escape";

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
        "System.Threading.CancellationToken", "System.Enum",
    };

    // The entry state (D1). Entry points: a parameter bound from the request (`[FromQuery]` and
    // the like) or of a controller action. Otherwise a parameter of a method visible outside the
    // assembly (it and every containing type public or protected) is untrusted with its source
    // marked PublicParameter. Safe types and test methods' parameters start clean, and a numeric
    // parameter starts Numeric unless a `[Range]` attribute bounds it.
    public static TaintState Seed(IMethodSymbol? owner)
    {
        var state = new TaintState();
        if (owner is null || TestMethods.IsTest(owner))
        {
            return state;
        }

        var name = MemberName(owner);
        var action = IsAction(owner);
        var visible = IsVisibleOutside(owner);
        foreach (var parameter in owner.Parameters)
        {
            var label = HasAttribute(parameter, RangeAttribute) && IsNumeric(parameter.Type) ? TaintLabel.None : LabelFor(parameter.Type);
            var source = (BindingOf(parameter), action, visible) switch
            {
                ({ } binding, _, _) => new TaintSource(parameter.Name, $"`[{binding}]` parameter of `{name}`", Line(parameter)),
                (null, true, _) => new TaintSource(parameter.Name, $"parameter of action `{name}`", Line(parameter)),
                (null, false, true) => new TaintSource(parameter.Name, $"parameter of public `{name}`", Line(parameter), PublicParameter: true),
                _ => null,
            };
            if (label != TaintLabel.None && source is not null)
            {
                state.Set(parameter, Taint.From(label, source));
            }
        }

        return state;
    }

    // A public instance method of a controller (a class deriving from ControllerBase or marked
    // [ApiController]) that is not [NonAction]. Names are compared simply, so an unrestored project
    // whose ASP.NET types do not resolve still counts.
    private static bool IsAction(IMethodSymbol method) =>
        method is { MethodKind: MethodKind.Ordinary, DeclaredAccessibility: Accessibility.Public, IsStatic: false }
        && !HasAttribute(method, NonActionAttribute)
        && IsController(method.ContainingType);

    private static bool IsController(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.Name == ControllerBaseType || HasAttribute(current, ApiControllerAttribute))
            {
                return true;
            }
        }

        return false;
    }

    // `FromQuery` for a parameter marked [FromQuery], and so on; null for any other parameter.
    private static string? BindingOf(IParameterSymbol parameter) =>
        BindingAttributes.FirstOrDefault(binding => HasAttribute(parameter, binding));

    private static bool HasAttribute(ISymbol symbol, string name) =>
        symbol.GetAttributes().Any(attribute => attribute.AttributeClass?.Name is { } written
            && (written == name || written == name + AttributeSuffix));

    // The taint a call returns when it is a source, or null for any other call:
    // `Console.ReadLine()` and `Console.In.ReadLine()` and their async forms are console input;
    // an exception's `ToString()` is ExceptionDetail, which only no-stack-trace-disclosure accepts.
    public static Taint? SourceOf(IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;
        var line = LineOf(invocation.Syntax);
        if (method.Name == nameof(ToString) && invocation.Instance is { } exception && Symbols.Is(exception.Type, ExceptionType))
        {
            return Taint.From(TaintLabel.ExceptionDetail, new TaintSource($"{exception.Syntax}.{nameof(ToString)}()", ExceptionDetail, line));
        }

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
        return name is null ? null : Taint.From(LabelFor(invocation.Type), new TaintSource(name, "console input", line));
    }

    // Property sources (D1): the parts of an ASP.NET HttpRequest are request input; and, scoped to
    // their rows, an exception's StackTrace is ExceptionDetail and the name of a zip or tar entry is
    // ArchiveEntry, which only zip-slip accepts.
    public static Taint? SourceOf(IPropertyReferenceOperation property)
    {
        var (label, description) = (property.Property.Name, Symbols.QualifiedName(property.Property.ContainingType)) switch
        {
            (var part, _) when RequestParts.Contains(part) && IsRequest(property.Property.ContainingType) => (TaintLabel.Untrusted, "request input"),
            (StackTraceProperty, _) when Symbols.Is(property.Property.ContainingType, ExceptionType) => (TaintLabel.ExceptionDetail, ExceptionDetail),
            (EntryFullNameProperty, ZipEntryType) or (EntryNameProperty, TarEntryType) => (TaintLabel.ArchiveEntry, "archive entry name"),
            _ => (TaintLabel.None, string.Empty),
        };
        var name = property.Instance is { } instance ? $"{instance.Syntax}.{property.Property.Name}" : property.Property.Name;
        return label == TaintLabel.None ? null : Taint.From(label, new TaintSource(name, description, LineOf(property.Syntax)));
    }

    private static bool IsRequest(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.Name == RequestType)
            {
                return true;
            }
        }

        return false;
    }

    private static int LineOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

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
    // Numeric; Regex.Escape makes the text literal. Null when the call is not a sanitizer.
    public static Taint? Sanitize(IInvocationOperation invocation, Taint input)
    {
        var method = invocation.TargetMethod;
        var type = Symbols.QualifiedName(method.ContainingType);
        if (IsParse(method) || type == ConvertType)
        {
            return Retype(invocation.Type, input);
        }

        if (type == RegexType && method.Name == EscapeMethod)
        {
            return Taint.None;
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

    // A value of this type cannot carry an injection: numbers, bool, enums (also as `Enum`), GUIDs,
    // dates, time spans, cancellation tokens and nullables of those. A char can still be a quote.
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

    private static bool IsVisibleOutside(ISymbol member)
    {
        for (ISymbol? symbol = member; symbol is not null and not INamespaceSymbol; symbol = symbol.ContainingSymbol)
        {
            if (symbol.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
            {
                return false;
            }
        }

        return true;
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

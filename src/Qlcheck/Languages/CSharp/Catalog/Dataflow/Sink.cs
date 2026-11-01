// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// A place where tainted data does harm: an argument of a method or constructor, or a property set,
// on a type named by its qualified metadata name. Derived types and implemented interfaces match
// too (Symbols.Is). The parameter is matched by name, not position, so overload resolution picks
// the right one: `Regex(pattern, options, matchTimeout)` still sinks only `pattern`.
internal sealed record Sink(SinkKind Kind, string Type, string Member, string? Parameter)
{
    private const char Alternative = '|';

    private const char Wildcard = '*';

    // Member is a method name, alternatives separated by `|`, each with an optional trailing `*`;
    // parameter names take alternatives too.
    public static Sink Call(string type, string member, string parameter) => new(SinkKind.Call, type, member, parameter);

    public static Sink Set(string type, string property) => new(SinkKind.Set, type, property, null);

    public static Sink Ctor(string type, string parameter) => new(SinkKind.Ctor, type, WellKnownMemberNames.InstanceConstructorName, parameter);

    public static Sink ArraySize() => new(SinkKind.ArraySize, string.Empty, string.Empty, null);

    public static Sink LoopBound() => new(SinkKind.LoopBound, string.Empty, string.Empty, null);

    public bool IsStructural => Kind is SinkKind.ArraySize or SinkKind.LoopBound;

    // Whether the program started must be a constant command shell (`sh`, `cmd.exe`, ...), or must
    // not be: the `fileName` argument of the same call, or a `FileName` set on the same object.
    public ProgramKind Program { get; init; }

    public Sink Shell() => this with { Program = ProgramKind.Shell };

    public Sink NotShell() => this with { Program = ProgramKind.NotShell };

    // The call's receiver must be this property: `ProcessStartInfo.ArgumentList` for `Add`.
    public (string Type, string Property)? Receiver { get; init; }

    public Sink On(string type, string property) => this with { Receiver = (type, property) };

    public bool ReceiverMatches(IOperation? instance) =>
        Receiver is not { } receiver
        || Unwrap(instance) is IPropertyReferenceOperation reference
            && reference.Property.Name == receiver.Property
            && Symbols.Is(reference.Property.ContainingType, receiver.Type);

    // An overload with a parameter of this name is not a sink: `Regex(pattern, options,
    // matchTimeout)` bounds the work an untrusted pattern can cause.
    public string? Excluded { get; init; }

    public Sink Without(string parameter) => this with { Excluded = parameter };

    // The parameter must be declared with exactly this type (qualified metadata name):
    // `Process.Start(fileName, arguments)` takes a command line as a string and an argument list
    // as an IEnumerable<string>, and a string is itself an IEnumerable<char>.
    public string? ParameterType { get; init; }

    public Sink OfType(string type) => this with { ParameterType = type };

    // D2: when the receiver's type does not resolve (an unrestored package), the sink matches by
    // member name and by the type's name ending in this: by default the sink type's core name
    // (`Command` for IDbCommand, `Logger` for LoggerExtensions); Unresolved overrides it for an
    // extension class whose receiver is another type (Dapper's SqlMapper extends `...Connection`).
    public string? UnresolvedReceiver { get; init; }

    public Sink Unresolved(string receiverSuffix) => this with { UnresolvedReceiver = receiverSuffix };

    public string UnresolvedSuffix => UnresolvedReceiver ?? CoreTypeName();

    public bool MatchesUnresolved(SinkKind kind, string member, string? typeName) =>
        kind == Kind
        && typeName is not null
        && typeName.EndsWith(UnresolvedSuffix, StringComparison.Ordinal)
        && (kind == SinkKind.Ctor || NamePatterns().Any(pattern => NameMatches(pattern, member)));

    // `member` is the called method or constructor, or the set property; `parameter` is the name
    // of the parameter the tainted argument binds to (null for a property set).
    public bool Matches(ISymbol member, string? parameter) =>
        Targets(member)
        && (Kind == SinkKind.Set || ParameterMatches(parameter))
        && member is not ITypeSymbol
        && !(member is IMethodSymbol method && method.Parameters.Any(item => item.Name == Excluded))
        && (ParameterType is null || member is IMethodSymbol typed && typed.Parameters.Any(item => item.Name == parameter && Symbols.QualifiedName(item.Type) == ParameterType));

    // Parameter names, like member names, take `|` alternatives: `path|sourceFileName`.
    private bool ParameterMatches(string? parameter) =>
        parameter is not null && Parameter is not null && Parameter.Split(Alternative).Contains(parameter, StringComparer.Ordinal);

    // Whether `symbol` is this sink's method or property, whatever the argument; for a constructor
    // sink, the constructor or the type itself (`new ProcessStartInfo(...)` names the type).
    public bool Targets(ISymbol symbol) =>
        Kind switch
        {
            SinkKind.Call => symbol is IMethodSymbol { MethodKind: not MethodKind.Constructor } method
                && NamePatterns().Any(pattern => NameMatches(pattern, method.Name)) && Symbols.Is(method.ContainingType, Type),
            SinkKind.Ctor => symbol switch
            {
                IMethodSymbol { MethodKind: MethodKind.Constructor } constructor => Symbols.Is(constructor.ContainingType, Type),
                ITypeSymbol type => Symbols.Is(type, Type),
                _ => false,
            },
            SinkKind.Set => symbol is IPropertySymbol property && property.Name == Member && Symbols.Is(property.ContainingType, Type),
            _ => false,
        };

    // The identifiers that can name this sink in source: the method or property name, each
    // alternative possibly ending in `*`, or a constructor sink's type name.
    public IEnumerable<string> NamePatterns() =>
        Kind switch
        {
            SinkKind.Ctor => [SimpleTypeName()],
            SinkKind.Call or SinkKind.Set => Member.Split(Alternative),
            _ => [],
        };

    // IDbCommand -> Command, DbConnection -> Connection, LoggerExtensions -> Logger.
    private string CoreTypeName()
    {
        const string Interface = "I";
        const string Database = "Db";
        const string Extensions = "Extensions";
        var name = SimpleTypeName();
        name = name.Length > 1 && name.StartsWith(Interface, StringComparison.Ordinal) && char.IsUpper(name[1]) ? name[1..] : name;
        name = name.StartsWith(Database, StringComparison.Ordinal) ? name[Database.Length..] : name;
        return name.EndsWith(Extensions, StringComparison.Ordinal) ? name[..^Extensions.Length] : name;
    }

    // `System.Collections.Generic.List`1` is written `List`, `Outer+Inner` is written `Inner`.
    private string SimpleTypeName()
    {
        var name = Type[(Type.LastIndexOfAny(['.', '+']) + 1)..];
        var arity = name.IndexOf('`', StringComparison.Ordinal);
        return arity < 0 ? name : name[..arity];
    }

    public static bool IsWildcard(string pattern) => pattern.EndsWith(Wildcard);

    private static IOperation? Unwrap(IOperation? operation) =>
        operation is IConversionOperation conversion ? Unwrap(conversion.Operand) : operation;

    public static bool NameMatches(string pattern, string name) =>
        pattern.EndsWith(Wildcard) ? name.StartsWith(pattern[..^1], StringComparison.Ordinal) : name == pattern;
}

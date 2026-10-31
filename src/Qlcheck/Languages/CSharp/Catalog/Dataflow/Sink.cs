// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
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

    // Member is a method name, alternatives separated by `|`, each with an optional trailing `*`.
    public static Sink Call(string type, string member, string parameter) => new(SinkKind.Call, type, member, parameter);

    public static Sink Set(string type, string property) => new(SinkKind.Set, type, property, null);

    public static Sink Ctor(string type, string parameter) => new(SinkKind.Ctor, type, WellKnownMemberNames.InstanceConstructorName, parameter);

    // Only a sink when the program started is a constant command shell (`sh`, `cmd.exe`, ...): the
    // `fileName` argument of the same call, or a `FileName` set earlier on the same object.
    public bool ShellOnly { get; init; }

    public Sink Shell() => this with { ShellOnly = true };

    // `member` is the called method or constructor, or the set property; `parameter` is the name
    // of the parameter the tainted argument binds to (null for a property set).
    public bool Matches(ISymbol member, string? parameter) =>
        Targets(member) && (Kind == SinkKind.Set || parameter == Parameter) && member is not ITypeSymbol;

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
            _ => symbol is IPropertySymbol property && property.Name == Member && Symbols.Is(property.ContainingType, Type),
        };

    // The identifiers that can name this sink in source: the method or property name, each
    // alternative possibly ending in `*`, or a constructor sink's type name.
    public IEnumerable<string> NamePatterns() =>
        Kind == SinkKind.Ctor ? [Type[(Type.LastIndexOf('.') + 1)..]] : Member.Split(Alternative);

    public static bool IsWildcard(string pattern) => pattern.EndsWith(Wildcard);

    public static bool NameMatches(string pattern, string name) =>
        pattern.EndsWith(Wildcard) ? name.StartsWith(pattern[..^1], StringComparison.Ordinal) : name == pattern;
}

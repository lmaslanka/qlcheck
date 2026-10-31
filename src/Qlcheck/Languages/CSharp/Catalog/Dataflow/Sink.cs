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

    // `member` is the called method or constructor, or the set property; `parameter` is the name
    // of the parameter the tainted argument binds to (null for a property set).
    public bool Matches(ISymbol member, string? parameter) =>
        Kind switch
        {
            SinkKind.Call => member is IMethodSymbol { MethodKind: not MethodKind.Constructor } method
                && NameMatches(method.Name) && parameter == Parameter && Symbols.Is(method.ContainingType, Type),
            SinkKind.Ctor => member is IMethodSymbol { MethodKind: MethodKind.Constructor } ctor
                && parameter == Parameter && Symbols.Is(ctor.ContainingType, Type),
            _ => member is IPropertySymbol property && property.Name == Member && Symbols.Is(property.ContainingType, Type),
        };

    // The member names this sink can match, for a cheap syntactic prefilter: a body without one of
    // these identifiers cannot reach the sink.
    public IEnumerable<string> Names() =>
        Kind == SinkKind.Ctor ? [Type[(Type.LastIndexOf('.') + 1)..]] : Member.Split(Alternative).Select(name => name.TrimEnd(Wildcard));

    private bool NameMatches(string name) =>
        Member.Split(Alternative).Any(pattern => pattern.EndsWith(Wildcard)
            ? name.StartsWith(pattern[..^1], StringComparison.Ordinal)
            : name == pattern);
}

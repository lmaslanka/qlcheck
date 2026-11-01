// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// Decides, before a body is bound, whether it can reach one of the sinks: one of its identifiers
// has a sink's name and binds to that sink's member. Binding a whole body for the dataflow is the
// expensive part; resolving one name is cheap, and names such as `Start` or `Arguments` are
// common in code that never starts a process.
internal sealed class SinkFilter
{
    // Names that read a source in a body: console input, an ASP.NET request, an archive's entries.
    private static readonly HashSet<string> SourceNames = new(StringComparer.Ordinal)
    {
        "ReadLine", "ReadLineAsync", "Request", "Entries", "FullName", "GetNextEntry",
    };

    private readonly Dictionary<string, List<Sink>> _exact = new(StringComparer.Ordinal);

    private readonly List<(string Pattern, Sink Sink)> _wildcards = [];

    private readonly List<string> _unresolvedTypes = [];

    private readonly bool _arraySizes;

    private readonly bool _loopBounds;

    private readonly bool _publicParameters;

    public SinkFilter(IEnumerable<Sink> sinks, bool publicParameters = true)
    {
        _publicParameters = publicParameters;
        foreach (var sink in sinks)
        {
            _arraySizes |= sink.Kind == SinkKind.ArraySize;
            _loopBounds |= sink.Kind == SinkKind.LoopBound;
            if (sink.Kind == SinkKind.Ctor)
            {
                _unresolvedTypes.Add(sink.UnresolvedSuffix);
            }

            foreach (var pattern in sink.NamePatterns())
            {
                Index(pattern, sink);
            }
        }
    }

    // A body is worth building when taint can enter it and a name in it can reach a sink, or it
    // has a structural sink and can see an entry point: the structural sinks' rows (allocation,
    // loop bounds) count only entry points, and loops and arrays are everywhere.
    public bool Relevant(SemanticModel model, SyntaxNode declaration, IReadOnlyList<SyntaxToken> tokens)
    {
        var owner = model.GetDeclaredSymbol(declaration) switch
        {
            IMethodSymbol method => method,
            IPropertySymbol property => property.GetMethod,
            _ => null,
        };
        if (SeesEntryPoint(owner, tokens))
        {
            return tokens.Any(token => Reaches(model, token)) || tokens.Any(IsStructural);
        }

        return HasSeededParameter(owner) && tokens.Any(token => Reaches(model, token));
    }

    public bool Reaches(SemanticModel model, SyntaxToken token)
    {
        if (!token.IsKind(SyntaxKind.IdentifierToken) || token.Parent is not { } name)
        {
            return false;
        }

        var named = Named(token.ValueText).ToList();
        if (named.Count == 0)
        {
            return IsUnresolvedSinkType(model, name, token.ValueText);
        }

        var reachable = named.Where(sink => sink.Kind != SinkKind.Ctor || CreatesWithArguments(name)).ToList();
        if (reachable.Count == 0)
        {
            return false;
        }

        var info = model.GetSymbolInfo(name);
        IEnumerable<ISymbol> symbols = info.Symbol is { } symbol ? [symbol] : info.CandidateSymbols;
        var resolved = symbols.ToList();

        // D2: a sink's name that binds to nothing (an unrestored package) may still be the sink.
        return resolved.Count == 0 || resolved.Any(candidate => reachable.Any(sink => sink.Targets(candidate)));
    }

    // A constructor sink is reached only by `new T(...)` with an argument: the type's name alone
    // (`String.Join`, `new List<int>()`) passes nothing to it.
    private static bool CreatesWithArguments(SyntaxNode name)
    {
        var type = name;
        while (type.Parent is QualifiedNameSyntax qualified && qualified.Right == type)
        {
            type = qualified;
        }

        return type.Parent is ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: > 0 } creation && creation.Type == type;
    }

    // D2: a type that does not resolve, named like a constructor sink's type (`SqlCommand` for
    // DbCommand).
    private bool IsUnresolvedSinkType(SemanticModel model, SyntaxNode name, string text) =>
        CreatesWithArguments(name)
        && _unresolvedTypes.Any(suffix => text.EndsWith(suffix, StringComparison.Ordinal))
        && model.GetSymbolInfo(name).Symbol is null or ITypeSymbol { TypeKind: TypeKind.Error };

    // An entry point or a row-scoped source can reach this body: console or request input, an
    // action's or `[From*]` parameter, a caught exception, or an archive's entries.
    private static bool SeesEntryPoint(IMethodSymbol? owner, IReadOnlyList<SyntaxToken> tokens) =>
        tokens.Any(token => token.IsKind(SyntaxKind.CatchKeyword) || token.IsKind(SyntaxKind.IdentifierToken) && SourceNames.Contains(token.ValueText))
        || owner is not null && TaintModel.HasEntryPoint(owner);

    // Taint can enter through the owner's parameters: it is seeded, and a selected row counts
    // public parameters.
    private bool HasSeededParameter(IMethodSymbol? owner) =>
        _publicParameters && owner is not null && !TaintModel.Seed(owner).IsEmpty;

    // A structural sink has no name: a body can reach one if it sizes an array with something
    // other than a literal, or has a loop.
    private bool IsStructural(SyntaxToken token) =>
        _arraySizes && token.IsKind(SyntaxKind.OpenBracketToken) && token.Parent is ArrayRankSpecifierSyntax rank
            && rank.Sizes.Any(size => size is not (OmittedArraySizeExpressionSyntax or LiteralExpressionSyntax))
        || _loopBounds && token.Kind() is SyntaxKind.ForKeyword or SyntaxKind.WhileKeyword;

    private void Index(string pattern, Sink sink)
    {
        if (Sink.IsWildcard(pattern))
        {
            _wildcards.Add((pattern, sink));
            return;
        }

        if (!_exact.TryGetValue(pattern, out var list))
        {
            list = [];
            _exact[pattern] = list;
        }

        list.Add(sink);
    }

    private IEnumerable<Sink> Named(string identifier)
    {
        var exact = _exact.TryGetValue(identifier, out var list) ? list : [];
        return exact.Concat(_wildcards.Where(entry => Sink.NameMatches(entry.Pattern, identifier)).Select(entry => entry.Sink));
    }
}

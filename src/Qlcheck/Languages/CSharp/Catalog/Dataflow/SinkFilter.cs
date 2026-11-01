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
    private readonly Dictionary<string, List<Sink>> _exact = new(StringComparer.Ordinal);

    private readonly List<(string Pattern, Sink Sink)> _wildcards = [];

    private readonly bool _arraySizes;

    private readonly bool _loopBounds;

    public SinkFilter(IEnumerable<Sink> sinks)
    {
        foreach (var sink in sinks)
        {
            _arraySizes |= sink.Kind == SinkKind.ArraySize;
            _loopBounds |= sink.Kind == SinkKind.LoopBound;
            foreach (var pattern in sink.NamePatterns())
            {
                Index(pattern, sink);
            }
        }
    }

    public bool Reaches(SemanticModel model, SyntaxToken token)
    {
        if (IsStructural(token))
        {
            return true;
        }

        if (!token.IsKind(SyntaxKind.IdentifierToken) || token.Parent is not { } name)
        {
            return false;
        }

        var named = Named(token.ValueText).ToList();
        if (named.Count == 0)
        {
            return false;
        }

        var info = model.GetSymbolInfo(name);
        IEnumerable<ISymbol> symbols = info.Symbol is { } symbol ? [symbol] : info.CandidateSymbols;
        return symbols.Any(candidate => named.Any(sink => sink.Targets(candidate)));
    }

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

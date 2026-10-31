// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// Decides, before a body is bound, whether it can reach one of the sinks: one of its identifiers
// has a sink's name and binds to that sink's member. Binding a whole body for the dataflow is the
// expensive part; resolving one name is cheap, and names such as `Start` or `Arguments` are
// common in code that never starts a process.
internal sealed class SinkFilter
{
    private readonly Dictionary<string, List<Sink>> _exact = new(StringComparer.Ordinal);

    private readonly List<(string Pattern, Sink Sink)> _wildcards = [];

    public SinkFilter(IEnumerable<Sink> sinks)
    {
        foreach (var sink in sinks)
        {
            foreach (var pattern in sink.NamePatterns())
            {
                Index(pattern, sink);
            }
        }
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
            return false;
        }

        var info = model.GetSymbolInfo(name);
        IEnumerable<ISymbol> symbols = info.Symbol is { } symbol ? [symbol] : info.CandidateSymbols;
        return symbols.Any(candidate => named.Any(sink => sink.Targets(candidate)));
    }

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

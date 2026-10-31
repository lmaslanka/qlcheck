// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;

namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// One file's dataflow results, shared by every row that asks: the body graphs, and the taint hits
// on a set of sinks. TaintFlow runs once per body for all the sinks together, and each Taint row
// then picks out its own, so twenty rows do not mean twenty passes.
internal sealed class DataflowCache(SyntaxTree tree, SemanticModel model)
{
    private IReadOnlyList<Sink>? _hitsFor;

    private IReadOnlyList<SinkHit> _hits = [];

    public BodyGraphs Graphs { get; } = new(tree, model);

    public IReadOnlyList<SinkHit> Hits(IReadOnlyList<Sink> sinks)
    {
        if (!ReferenceEquals(sinks, _hitsFor))
        {
            var names = sinks.SelectMany(sink => sink.Names()).ToHashSet(StringComparer.Ordinal);
            _hits = [.. Graphs.Mentioning(names).SelectMany(body => TaintFlow.Run(body, sinks))];
            _hitsFor = sinks;
        }

        return _hits;
    }
}

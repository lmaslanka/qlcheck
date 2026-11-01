// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;

namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// One file's dataflow results, shared by every row that asks: the body graphs, and the taint hits
// on a set of sinks. TaintFlow runs once per body for the sinks of all selected rows together, and
// each Taint row then picks out its own, so twenty rows do not mean twenty passes.
internal sealed class DataflowCache(SyntaxTree tree, SemanticModel model)
{
    private IReadOnlyList<Sink>? _hitsFor;

    private IReadOnlyList<SinkHit> _hits = [];

    public BodyGraphs Graphs { get; } = new(tree, model);

    // The sinks of the Flow rows this run selected, and whether any of them counts public
    // parameters (TaintEngine sets both); all rows otherwise.
    public IReadOnlyList<Sink> Sinks { get; set; } = TaintEngine.FlowSinks;

    public bool PublicParameters { get; set; } = true;

    public IReadOnlyList<SinkHit> Hits(IReadOnlyList<Sink> sinks)
    {
        if (!ReferenceEquals(sinks, _hitsFor))
        {
            var filter = new SinkFilter(sinks, PublicParameters);
            _hits = [.. Graphs.Where((body, tokens) => filter.Relevant(model, body, tokens)).SelectMany(body => TaintFlow.Run(body, sinks))];
            _hitsFor = sinks;
        }

        return _hits;
    }
}

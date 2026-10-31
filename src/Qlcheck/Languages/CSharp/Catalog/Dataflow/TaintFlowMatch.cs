// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// A Row.Flow row: reports the file's taint hits on the row's own sinks that carry a label the row
// accepts, once per sink node however many sources reach it.
internal static class TaintFlowMatch
{
    public static void Report(WalkContext ctx, string id, TaintSpec spec)
    {
        var hits = ctx.Dataflow.Hits(TaintEngine.FlowSinks)
            .Where(hit => (hit.Labels & spec.Accepts) != TaintLabel.None && spec.Sinks.Contains(hit.Sink))
            .DistinctBy(hit => hit.Node);
        foreach (var hit in hits)
        {
            ctx.Report(id, hit.Node);
        }
    }
}

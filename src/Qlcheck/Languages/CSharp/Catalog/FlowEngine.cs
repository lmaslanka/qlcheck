// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis.CSharp;
using Qlcheck.Languages.CSharp.Catalog.Dataflow;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class FlowEngine
{
    public static WalkRow[] Rows { get; } =
    [
        Row.Handle("invariant-loop-bound", CheckClass.Flow, StylePatterns.ForBoundChanges),
        Row.Handle("lock-released", CheckClass.Flow, LockFlowMatch.NotReleasedOnAllPaths),
        Row.Handle("loop-condition-reachable", CheckClass.Flow, StylePatterns.ForNeverTrue),
        Row.Handle("loop-counter-direction", CheckClass.Flow, StylePatterns.ForWrongWay),
        Row.Handle("loop-more-than-once", CheckClass.Flow, StylePatterns.ForOnce),
        Row.Handle("loop-must-change-counter", CheckClass.Flow, StylePatterns.ForNoCounter),
        Row.Handle("matching-lock-release", CheckClass.Flow, LockFlowMatch.ModeMismatch),
        Row.Handle("no-infinite-loop", CheckClass.Flow, Reach.InfiniteLoop),
        Row.Handle("reachable-branch", CheckClass.Flow, Reach.UnreachableBranch),
        Row.Handle("release-lock-same-method", CheckClass.Flow, LockFlowMatch.NeverReleased),
        Row.Handle("validate-public-arguments", CheckClass.Flow, PublicArgumentPattern.Apply),
    ];

    public static void Apply(WalkContext ctx, IReadOnlySet<string> selected)
    {
        foreach (var row in Rows)
        {
            if (selected.Contains(row.Id))
            {
                row.Apply(ctx);
            }
        }
    }
}

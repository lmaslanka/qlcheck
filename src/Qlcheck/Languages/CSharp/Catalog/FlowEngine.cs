// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis.CSharp;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class FlowEngine
{
    public static readonly WalkRow[] Rows =
    [
        Row.Handle("invariant-loop-bound", CheckClass.Flow, StylePatterns.ForBoundChanges),
        Row.Invoke("lock-released", CheckClass.Flow, "Enter", "Monitor"),
        Row.Handle("loop-condition-reachable", CheckClass.Flow, StylePatterns.ForNeverTrue),
        Row.Handle("loop-counter-direction", CheckClass.Flow, StylePatterns.ForWrongWay),
        Row.Handle("loop-more-than-once", CheckClass.Flow, StylePatterns.ForOnce),
        Row.Handle("loop-must-change-counter", CheckClass.Flow, StylePatterns.ForNoCounter),
        Row.Invoke("matching-lock-release", CheckClass.Flow, "Exit", "Monitor"),
        Row.Handle("no-infinite-loop", CheckClass.Flow, Patterns.Infinite),
        Row.Handle("null-dereference", CheckClass.Flow, Patterns.NullDeref),
        Row.Handle("reachable-branch", CheckClass.Flow, Patterns.Unreachable),
        Row.Invoke("release-lock-same-method", CheckClass.Flow, "Enter", "Monitor"),
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

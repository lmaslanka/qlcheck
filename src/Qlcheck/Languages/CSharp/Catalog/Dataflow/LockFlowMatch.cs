// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// The three lock rows, each reporting its kind of LockFlow problem with the lock named.
internal static class LockFlowMatch
{
    public static void NotReleasedOnAllPaths(WalkContext ctx, string id) =>
        Report(ctx, id, LockProblem.NotReleasedOnAllPaths, "is still held when the method returns on some path; release it in a `finally`.");

    public static void NeverReleased(WalkContext ctx, string id) =>
        Report(ctx, id, LockProblem.NeverReleased, "is acquired here and released nowhere in this method.");

    public static void ModeMismatch(WalkContext ctx, string id) =>
        Report(ctx, id, LockProblem.ModeMismatch, "is not held in this mode here; release the mode that was entered.");

    private static void Report(WalkContext ctx, string id, LockProblem problem, string detail)
    {
        foreach (var hit in ctx.Dataflow.Locks.Where(hit => hit.Problem == problem))
        {
            ctx.Report(id, hit.Node, $"`{hit.Lock}` {detail}");
        }
    }
}

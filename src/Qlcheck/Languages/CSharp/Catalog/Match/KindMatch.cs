// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis.CSharp;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class KindMatch
{
    public static void One(WalkContext ctx, string id, SyntaxKind kind)
    {
        foreach (var node in ctx.Nodes(kind))
        {
            ctx.Report(id, node);
        }
    }

    public static void Any(WalkContext ctx, string id, SyntaxKind[] kinds)
    {
        foreach (var kind in kinds)
        {
            One(ctx, id, kind);
        }
    }
}

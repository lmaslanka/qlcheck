// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis.CSharp;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class MemberMatch
{
    public static void Report(WalkContext ctx, string id, string member, string? type)
    {
        foreach (var node in ctx.Nodes(SyntaxKind.SimpleMemberAccessExpression))
        {
            if (!Same(Names.Member(node), member))
            {
                continue;
            }

            if (type is not null && !Same(Names.MemberType(node), type))
            {
                continue;
            }

            ctx.Report(id, node);
        }
    }

    public static void Any(WalkContext ctx, string id, string[] members)
    {
        foreach (var member in members)
        {
            Report(ctx, id, member, null);
        }
    }

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.Ordinal);
}

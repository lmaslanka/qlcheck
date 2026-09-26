// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis.CSharp;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class Row
{
    public static WalkRow Kind(string id, CheckClass group, SyntaxKind kind) =>
        new()
        {
            Id = id,
            Class = group,
            Apply = ctx => KindMatch.One(ctx, id, kind),
        };

    public static WalkRow Kinds(string id, CheckClass group, params SyntaxKind[] kinds) =>
        new()
        {
            Id = id,
            Class = group,
            Apply = ctx => KindMatch.Any(ctx, id, kinds),
        };

    public static WalkRow Invoke(string id, CheckClass group, string method, string? type = null) =>
        new()
        {
            Id = id,
            Class = group,
            Apply = ctx => InvokeMatch.Report(ctx, id, method, type),
        };

    public static WalkRow Invokes(string id, CheckClass group, params string[] methods) =>
        new()
        {
            Id = id,
            Class = group,
            Apply = ctx => InvokeMatch.Any(ctx, id, methods),
        };

    public static WalkRow Create(string id, CheckClass group, string typeName) =>
        new()
        {
            Id = id,
            Class = group,
            Apply = ctx => CreateMatch.Report(ctx, id, typeName),
        };

    public static WalkRow Creates(string id, CheckClass group, params string[] typeNames) =>
        new()
        {
            Id = id,
            Class = group,
            Apply = ctx => CreateMatch.Any(ctx, id, typeNames),
        };

    public static WalkRow Member(string id, CheckClass group, string member, string? type = null) =>
        new()
        {
            Id = id,
            Class = group,
            Apply = ctx => MemberMatch.Report(ctx, id, member, type),
        };

    public static WalkRow Members(string id, CheckClass group, params string[] members) =>
        new()
        {
            Id = id,
            Class = group,
            Apply = ctx => MemberMatch.Any(ctx, id, members),
        };

    public static WalkRow Attribute(string id, CheckClass group, string name) =>
        new()
        {
            Id = id,
            Class = group,
            Apply = ctx => AttributeMatch.Report(ctx, id, name),
        };

    public static WalkRow Ident(string id, CheckClass group, string name) =>
        new()
        {
            Id = id,
            Class = group,
            Apply = ctx => IdentMatch.Report(ctx, id, name),
        };

    public static WalkRow Token(string id, CheckClass group, string text) =>
        new()
        {
            Id = id,
            Class = group,
            Apply = ctx => TokenMatch.Report(ctx, id, text),
        };

    public static WalkRow Trivia(string id, CheckClass group, string word) =>
        new()
        {
            Id = id,
            Class = group,
            Apply = ctx => TriviaMatch.Report(ctx, id, word),
        };

    public static WalkRow Metric(string id, CheckClass group, string metric) =>
        new()
        {
            Id = id,
            Class = group,
            Apply = ctx => MetricMatch.Report(ctx, id, metric),
        };

    public static WalkRow Taint(string id, CheckClass group, params string[] sinks) =>
        new()
        {
            Id = id,
            Class = group,
            Apply = ctx => TaintMatch.Report(ctx, id, sinks),
        };

    public static WalkRow Handle(string id, CheckClass group, Action<WalkContext, string> apply) =>
        new()
        {
            Id = id,
            Class = group,
            Apply = ctx => apply(ctx, id),
        };

    public static WalkRow Test(string id, CheckClass group, Action<WalkContext, string> apply) =>
        new()
        {
            Id = id,
            Class = group,
            Apply = ctx => ApplyTest(ctx, id, apply),
        };

    private static void ApplyTest(WalkContext ctx, string id, Action<WalkContext, string> apply)
    {
        if (!ctx.IsTestFile)
        {
            return;
        }

        apply(ctx, id);
    }
}

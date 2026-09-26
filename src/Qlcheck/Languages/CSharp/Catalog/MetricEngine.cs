// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis.CSharp;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class MetricEngine
{
    public static readonly WalkRow[] Rows =
    [
        Row.Metric("case-length", CheckClass.Metric, MetricNames.CaseLength),
        Row.Metric("class-coupling", CheckClass.Metric, MetricNames.Coupling),
        Row.Metric("cognitive-complexity", CheckClass.Metric, MetricNames.Cognitive),
        Row.Metric("duplicate-string", CheckClass.Metric, MetricNames.DuplicateString),
        Row.Metric("expression-complexity", CheckClass.Metric, MetricNames.Expression),
        Row.Metric("file-length", CheckClass.Metric, MetricNames.FileLength),
        Row.Metric("function-length", CheckClass.Metric, MetricNames.FunctionLength),
        Row.Metric("generic-arity", CheckClass.Metric, MetricNames.Arity),
        Row.Metric("inheritance-depth", CheckClass.Metric, MetricNames.Inheritance),
        Row.Metric("method-complexity", CheckClass.Metric, MetricNames.Cyclomatic),
        Row.Metric("nesting-depth", CheckClass.Metric, MetricNames.Nesting),
        Row.Metric("switch-too-many-cases", CheckClass.Metric, MetricNames.SwitchCases),
        Row.Metric("too-many-logs", CheckClass.Metric, MetricNames.Logs),
        Row.Metric("too-many-parameters", CheckClass.Metric, MetricNames.Parameters),
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

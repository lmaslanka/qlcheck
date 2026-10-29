using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class MetricMatchTests
{
    [Fact]
    public void Report_flags_method_with_too_many_type_parameters()
    {
        var ctx = MatchFixtures.Context("class C { void M<T1, T2, T3, T4>() { } }");
        MetricMatch.Report(ctx, MatchFixtures.Id, MetricNames.Arity);
        Assert.Contains(ctx.Findings, finding => finding.Message.Contains("(4)", StringComparison.Ordinal));
    }
}

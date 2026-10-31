using Qlcheck.Languages.CSharp.Catalog;
using Qlcheck.Languages.CSharp.Catalog.Dataflow;

namespace Qlcheck.Tests;

// The real sql-injection and command-injection specs, with a short hint.
public class TaintFlowMatchTests
{
    private const string Hint = "Use a parameter.";

    private static readonly TaintSpec Sql = Spec("sql-injection");

    private static readonly TaintSpec Command = Spec("command-injection");

    [Fact]
    public void The_message_names_the_source_the_sink_the_locals_and_the_hint()
    {
        var finding = Assert.Single(Run(
            Sql,
            "public class R {\n public void Delete(System.Data.Common.DbCommand c, string userId) { var q = \"x\" + userId; var sql = q; c.CommandText = sql; } }"));

        Assert.Equal("message `userId` (parameter of public `Delete`, line 2) reaches `DbCommand.CommandText` via `q`, `sql`. Use a parameter.", finding.Message);
    }

    [Theory]
    [InlineData("System.Diagnostics.Process.Start(file);", "reaches `Process.Start(fileName)`. ")]
    [InlineData("var p = new System.Diagnostics.ProcessStartInfo(file);", "reaches `new ProcessStartInfo(fileName)`. ")]
    public void A_call_or_constructor_sink_names_its_parameter(string body, string expected)
    {
        var finding = Assert.Single(Run(Command, $"public class R {{ public void M(string file) {{ {body} }} }}"));

        Assert.Contains(expected, finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Console_input_is_named_as_such()
    {
        var finding = Assert.Single(Run(Sql, "public class R { void M(System.Data.Common.DbCommand c) { c.CommandText = System.Console.ReadLine(); } }"));

        Assert.StartsWith("message `Console.ReadLine()` (console input, line 1) reaches", finding.Message, StringComparison.Ordinal);
    }

    private static IReadOnlyList<Finding> Run(TaintSpec spec, string source)
    {
        var ctx = MatchFixtures.Context(source);
        TaintFlowMatch.Report(ctx, MatchFixtures.Id, spec);
        return ctx.Findings;
    }

    private static TaintSpec Spec(string id) => TaintEngine.Rows.Single(row => row.Id == id).Taint! with { Hint = Hint };
}

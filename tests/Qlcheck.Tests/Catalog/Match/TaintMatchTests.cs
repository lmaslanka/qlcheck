using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class TaintMatchTests
{
    [Fact]
    public void Report_flags_tainted_argument_to_a_sink_constructor()
    {
        var ctx = MatchFixtures.Context(
            "class C { public void M(string userInput) { var x = new System.Text.StringBuilder(userInput); } }");
        TaintMatch.Report(ctx, MatchFixtures.Id, ["StringBuilder"]);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void Report_ignores_sink_creation_without_an_argument_list()
    {
        var ctx = MatchFixtures.Context(
            "class C { public void M() { var x = new System.Collections.Generic.List<int> { 1, 2, 3 }; } }");
        TaintMatch.Report(ctx, MatchFixtures.Id, ["List"]);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void Report_ignores_assignment_to_a_non_sink_member()
    {
        var ctx = MatchFixtures.Context(
            "class C { object NotASink; public void M(string userInput) { NotASink = userInput; } }");
        TaintMatch.Report(ctx, MatchFixtures.Id, ["CommandText"]);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void Report_flags_sink_assignment_of_a_tainted_parameter()
    {
        var ctx = MatchFixtures.Context(
            "class C { object CommandText; public void M(string userInput) { CommandText = userInput; } }");
        TaintMatch.Report(ctx, MatchFixtures.Id, ["CommandText"]);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void Report_flags_sink_assignment_from_console_read_line()
    {
        var ctx = MatchFixtures.Context(
            "class C { object CommandText; public void M() { CommandText = System.Console.ReadLine(); } }");
        TaintMatch.Report(ctx, MatchFixtures.Id, ["CommandText"]);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void Report_flags_sink_assignment_of_an_interpolated_string_using_a_tainted_parameter()
    {
        var ctx = MatchFixtures.Context(
            "class C { object CommandText; public void M(string userInput) { CommandText = $\"cmd {userInput}\"; } }");
        TaintMatch.Report(ctx, MatchFixtures.Id, ["CommandText"]);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void Report_flags_sink_assignment_of_a_concatenated_tainted_parameter()
    {
        var ctx = MatchFixtures.Context(
            "class C { object CommandText; public void M(string userInput) { CommandText = \"cmd \" + userInput; } }");
        TaintMatch.Report(ctx, MatchFixtures.Id, ["CommandText"]);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void Report_ignores_assignment_when_source_is_not_inside_a_method()
    {
        var ctx = MatchFixtures.Context(
            "class C { object CommandText; C(string userInput) { CommandText = userInput; } }");
        TaintMatch.Report(ctx, MatchFixtures.Id, ["CommandText"]);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void Report_ignores_assignment_when_enclosing_method_is_not_public()
    {
        var ctx = MatchFixtures.Context(
            "class C { object CommandText; void M(string userInput) { CommandText = userInput; } }");
        TaintMatch.Report(ctx, MatchFixtures.Id, ["CommandText"]);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void Report_ignores_assignment_of_a_local_variable_that_is_not_a_parameter()
    {
        var ctx = MatchFixtures.Context(
            "class C { object CommandText; public void M(string userInput) { var other = \"text\"; CommandText = other; } }");
        TaintMatch.Report(ctx, MatchFixtures.Id, ["CommandText"]);
        Assert.Empty(ctx.Findings);
    }
}

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

    [Fact]
    public void ReportOnTypes_flags_tainted_argument_on_an_allowed_receiver_type()
    {
        var ctx = MatchFixtures.Context(
            """
            class SqlCommand { public void Execute(string sql) { } }
            class C
            {
                public void M(string userInput)
                {
                    var cmd = new SqlCommand();
                    cmd.Execute(userInput);
                }
            }
            """);
        TaintMatch.ReportOnTypes(ctx, MatchFixtures.Id, ["SqlCommand"], ["Execute"]);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void ReportOnTypes_ignores_a_call_on_a_receiver_type_that_is_not_allowed()
    {
        var ctx = MatchFixtures.Context(
            """
            class UpdateRequisitionCommand { public void Execute(string recordId) { } }
            class C
            {
                public void M(UpdateRequisitionCommand updateRequisitionCommand, string recordId)
                {
                    updateRequisitionCommand.Execute(recordId);
                }
            }
            """);
        TaintMatch.ReportOnTypes(ctx, MatchFixtures.Id, ["SqlCommand"], ["Execute"]);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void Report_ignores_a_non_string_typed_parameter()
    {
        var ctx = MatchFixtures.Context(
            "class C { public System.Threading.Tasks.Task M(System.Net.Http.HttpClient client, System.Guid recordId) "
            + "=> client.GetAsync(recordId); }");
        TaintMatch.Report(ctx, MatchFixtures.Id, ["GetAsync"]);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void Report_flags_a_string_typed_parameter_reaching_a_sink()
    {
        var ctx = MatchFixtures.Context(
            "class C { public System.Threading.Tasks.Task M(System.Net.Http.HttpClient client, string url) "
            + "=> client.GetAsync(url); }");
        TaintMatch.Report(ctx, MatchFixtures.Id, ["GetAsync"]);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void Report_ignores_a_nullable_non_string_typed_parameter()
    {
        var ctx = MatchFixtures.Context(
            "class C { object CommandText; public void M(int? recordId) { CommandText = recordId; } }");
        TaintMatch.Report(ctx, MatchFixtures.Id, ["CommandText"]);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ReportOnTypes_ignores_a_bare_invocation_with_no_receiver()
    {
        var ctx = MatchFixtures.Context(
            "class C { public void Execute(string sql) { } public void M(string userInput) { Execute(userInput); } }");
        TaintMatch.ReportOnTypes(ctx, MatchFixtures.Id, ["SqlCommand"], ["Execute"]);
        Assert.Empty(ctx.Findings);
    }
}

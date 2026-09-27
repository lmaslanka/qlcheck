using Microsoft.CodeAnalysis.CSharp;
using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class RowTests
{
    [Fact]
    public void Kind_row_reports_matching_syntax_kind()
    {
        var ctx = MatchFixtures.Context("class C { void M() { ; } }");
        var row = Row.Kind(MatchFixtures.Id, CheckClass.Syntax, SyntaxKind.EmptyStatement);
        row.Apply(ctx);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void Invokes_row_reports_every_matching_method()
    {
        var ctx = MatchFixtures.Context(
            "class C { void M() { System.Console.WriteLine(); System.Console.Write(); } }");
        var row = Row.Invokes(MatchFixtures.Id, CheckClass.Syntax, "WriteLine", "Write");
        row.Apply(ctx);
        Assert.Equal(2, ctx.Findings.Count);
    }

    [Fact]
    public void Create_row_reports_matching_created_type()
    {
        var ctx = MatchFixtures.Context("class C { void M() { var x = new System.Text.StringBuilder(); } }");
        var row = Row.Create(MatchFixtures.Id, CheckClass.Syntax, "StringBuilder");
        row.Apply(ctx);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void Creates_row_reports_every_matching_created_type()
    {
        var ctx = MatchFixtures.Context(
            "class C { void M() { var a = new object(); var b = new System.Text.StringBuilder(); } }");
        var row = Row.Creates(MatchFixtures.Id, CheckClass.Syntax, "object", "StringBuilder");
        row.Apply(ctx);
        Assert.Equal(2, ctx.Findings.Count);
    }

    [Fact]
    public void Member_row_reports_matching_member_access()
    {
        var ctx = MatchFixtures.Context("class C { void M() { var x = System.Console.Out; } }");
        var row = Row.Member(MatchFixtures.Id, CheckClass.Syntax, "Out", "Console");
        row.Apply(ctx);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void Members_row_reports_every_matching_member_access()
    {
        var ctx = MatchFixtures.Context(
            "class C { void M() { var a = System.Console.Out; var b = System.Console.Error; } }");
        var row = Row.Members(MatchFixtures.Id, CheckClass.Syntax, "Out", "Error");
        row.Apply(ctx);
        Assert.Equal(2, ctx.Findings.Count);
    }

    [Fact]
    public void Token_row_reports_matching_token_text()
    {
        var ctx = MatchFixtures.Context("class C { void M(int value) { } }");
        var row = Row.Token(MatchFixtures.Id, CheckClass.Syntax, "value");
        row.Apply(ctx);
        Assert.Single(ctx.Findings);
    }
}

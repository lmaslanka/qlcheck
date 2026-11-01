using Qlcheck.Languages.CSharp.Catalog;
using Qlcheck.Languages.CSharp.Catalog.Dataflow;

namespace Qlcheck.Tests;

// D2: when a package is not restored its types are errors and calls do not bind. A sink is then
// matched by member name and by the unresolved type's name ending in the sink type's core name
// (`SqlCommand` for IDbCommand), with only the first argument counted.
public class UnresolvedSinkTests
{
    private const string Sql = "sql-injection";

    [Theory]
    [InlineData("var c = new SqlCommand(); c.CommandText = \"SELECT \" + userId;", "c.CommandText = \"SELECT \" + userId")]
    [InlineData("var c = new SqlCommand(\"SELECT \" + userId, null);", "new SqlCommand(\"SELECT \" + userId, null)")]
    [InlineData("var c = new NpgsqlConnection(); c.Query<int>(\"SELECT \" + userId);", "c.Query<int>(\"SELECT \" + userId)")]
    public void An_unresolved_call_set_or_creation_matches_by_name(string body, string at)
    {
        var source = $"public class R {{ public void M(string userId) {{ {body} }} }}";

        Assert.Equal([source.IndexOf(at, StringComparison.Ordinal) + 1], Run(Sql, source));
    }

    [Fact]
    public void An_unresolved_logger_matches_only_its_template()
    {
        const string Source = "public class R { ILogger<R> _log = null!; public void M() { var name = System.Console.ReadLine(); _log.LogInformation(\"User \" + name); _log.LogInformation(\"User {Name}\", name); } }";

        Assert.Equal([Source.IndexOf("_log.LogInformation(\"User \" + name)", StringComparison.Ordinal) + 1], Run("log-injection", Source));
    }

    [Theory]
    [InlineData("var c = new SqlCommand(); c.Text = userId;")]
    [InlineData("var c = new Widget(); c.CommandText = userId;")]
    [InlineData("var c = new SqlCommand(); c.CommandText = \"SELECT 1\";")]
    public void Other_names_and_clean_values_do_not_match(string body)
    {
        Assert.Empty(Run(Sql, $"public class R {{ public void M(string userId) {{ {body} }} }}"));
    }

    // Sources are one line, so a Finding is identified by the column it starts at.
    private static List<int> Run(string id, string source)
    {
        var file = new SourceFile("Repo.cs", source);
        var tree = CSharpTrees.Parse(file);
        var model = CSharpCompilations.Create("unresolved", [tree]).GetSemanticModel(tree);
        var ctx = WalkContext.Create(file, tree, model, new Dictionary<string, string> { [id] = id });
        TaintFlowMatch.Report(ctx, id, TaintEngine.Rows.Single(row => row.Id == id).Taint!);
        return ctx.Findings.Select(finding => finding.Column).ToList();
    }
}

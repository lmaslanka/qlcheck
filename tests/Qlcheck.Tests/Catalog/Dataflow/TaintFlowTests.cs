using Qlcheck.Languages.CSharp.Catalog.Dataflow;

namespace Qlcheck.Tests;

public class TaintFlowTests
{
    private const string Header = "using System; using System.Data.Common; using System.Diagnostics;\n";

    private const string StartInfo = "System.Diagnostics.ProcessStartInfo";

    private const string FileName = "fileName";

    private const string UserId = "userId";

    private static readonly Sink CommandText = Sink.Set("System.Data.Common.DbCommand", nameof(System.Data.Common.DbCommand.CommandText));

    private static readonly Sink[] Sinks =
    [
        CommandText,
        Sink.Call("System.Diagnostics.Process", "Start", FileName),
        Sink.Ctor(StartInfo, FileName),
        Sink.Set(StartInfo, "FileName"),
    ];

    [Fact]
    public void A_public_parameter_reaching_a_sink_is_a_hit_naming_its_source()
    {
        var hit = Assert.Single(Run("public class R { public void M(DbCommand c, string userId) { c.CommandText = userId; } }"));

        Assert.Equal(CommandText, hit.Sink);
        Assert.Equal("c.CommandText = userId", hit.Node.ToString());
        Assert.Equal(TaintLabel.Untrusted, hit.Labels);
        Assert.Equal(new TaintSource(UserId, "parameter of public `M`", 2), hit.Source);
        Assert.Empty(hit.Via);
    }

    [Theory]
    [InlineData("var query = \"SELECT \" + userId; c.CommandText = query;")]
    [InlineData("var query = $\"SELECT {userId}\"; c.CommandText = query;")]
    [InlineData("var query = \"SELECT 1\"; if (userId.Length > 0) { query = \"SELECT \" + userId; } c.CommandText = query;")]
    [InlineData("var query = \"SELECT 1\"; try { query = userId; } finally { c.CommandText = query; }")]
    public void Taint_follows_locals_concatenation_interpolation_and_branches(string body)
    {
        var hit = Assert.Single(Run($"public class R {{ public void M(DbCommand c, string userId) {{ {body} }} }}"));

        Assert.Equal(["query"], hit.Via);
    }

    [Theory]
    [InlineData("public void M(DbCommand c, string userId) { var q = userId; q = \"SELECT 1\"; c.CommandText = q; }")]
    [InlineData("public void M(DbCommand c, string userId) { c.CommandText = \"SELECT 1\"; }")]
    [InlineData("public void M(DbCommand c, Guid userId) { c.CommandText = \"SELECT \" + userId; }")]
    [InlineData("public void M(DbCommand c, int? userId) { c.CommandText = $\"SELECT {userId}\"; }")]
    [InlineData("private void M(DbCommand c, string userId) { c.CommandText = userId; }")]
    [InlineData("internal void M(DbCommand c, string userId) { c.CommandText = userId; }")]
    public void Clean_values_kills_safe_types_and_non_public_methods_give_nothing(string member)
    {
        Assert.Empty(Run($"public class R {{ {member} }}"));
    }

    [Fact]
    public void A_numeric_parameter_carries_the_numeric_label()
    {
        var hit = Assert.Single(Run("public class R { public void M(DbCommand c, int n) { c.CommandText = \"SELECT \" + n.ToString(); c.CommandText = (string)(object)n; } }"));

        Assert.Equal(TaintLabel.Numeric, hit.Labels);
    }

    [Fact]
    public void Console_input_is_a_source()
    {
        var hit = Assert.Single(Run("public class R { void M(DbCommand c) { var s = Console.ReadLine(); c.CommandText = \"SELECT \" + s; } }"));

        Assert.Equal("Console.ReadLine()", hit.Source.Name);
    }

    [Theory]
    [InlineData("Process.Start(file);", "Process.Start(file)")]
    [InlineData("var psi = new ProcessStartInfo(file);", "new ProcessStartInfo(file)")]
    [InlineData("var psi = new ProcessStartInfo { FileName = file, UseShellExecute = true };", "FileName = file")]
    [InlineData("var p = new Process(); p.StartInfo.FileName = file;", "p.StartInfo.FileName = file")]
    public void Calls_constructors_initializers_and_nested_receivers_are_sinks(string body, string at)
    {
        var hit = Assert.Single(Run($"public class R {{ public void M(string file) {{ {body} }} }}"));

        Assert.Equal(at, hit.Node.ToString());
    }

    [Fact]
    public void A_type_that_only_shares_the_sink_name_is_not_a_sink()
    {
        Assert.Empty(Run("public class Process { public static void Start(string fileName) { } } public class R { public void M(string file) { Process.Start(file); } }"));
    }

    private static IReadOnlyList<SinkHit> Run(string source)
    {
        var tree = CSharpTrees.Parse(new SourceFile("Repo.cs", Header + source));
        var model = CSharpCompilations.Create("taint", [tree]).GetSemanticModel(tree);
        var errors = model.GetDiagnostics().Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        Assert.Empty(errors);
        var names = Sinks.SelectMany(sink => sink.Names()).ToHashSet(StringComparer.Ordinal);
        return new BodyGraphs(tree, model).Mentioning(names).SelectMany(body => TaintFlow.Run(body, Sinks)).ToList();
    }
}

using Qlcheck.Languages.CSharp.Catalog.Dataflow;

namespace Qlcheck.Tests;

public class SinkFilterTests
{
    private static readonly SinkFilter Filter = new(
    [
        Sink.Call("System.Diagnostics.Process", "Start", "fileName"),
        Sink.Call("System.IO.File", "Read*", "path"),
        Sink.Ctor("System.Diagnostics.ProcessStartInfo", "fileName"),
        Sink.Set("System.Diagnostics.ProcessStartInfo", "Arguments"),
    ]);

    [Theory]
    [InlineData("System.Diagnostics.Process.Start(s);", "Start")]
    [InlineData("System.IO.File.ReadAllText(s);", "ReadAllText")]
    [InlineData("var p = new System.Diagnostics.ProcessStartInfo(s);", "ProcessStartInfo")]
    [InlineData("var p = new System.Diagnostics.ProcessStartInfo(); p.Arguments = s;", "Arguments")]
    public void A_name_that_binds_to_a_sink_reaches_it(string body, string name)
    {
        Assert.True(Reaches($"class C {{ void M(string s) {{ {body} }} }}", name));
    }

    [Theory]
    [InlineData("class Worker { public void Start(string s) { } } class C { void M(Worker w, string s) { w.Start(s); } }", "Start")]
    [InlineData("class Args { public string[] Arguments = []; } class C { void M(Args a) { var n = a.Arguments.Length; } }", "Arguments")]
    [InlineData("class C { void M(string s) { System.IO.File.Exists(s); } }", "Exists")]
    public void A_name_that_binds_elsewhere_does_not(string source, string name)
    {
        Assert.False(Reaches(source, name));
    }

    private static bool Reaches(string source, string name)
    {
        var tree = CSharpTrees.Parse(new SourceFile("Repo.cs", source));
        var model = CSharpCompilations.Create("filter", [tree]).GetSemanticModel(tree);
        var token = tree.GetRoot().DescendantTokens().Last(candidate => candidate.ValueText == name);
        return Filter.Reaches(model, token);
    }
}

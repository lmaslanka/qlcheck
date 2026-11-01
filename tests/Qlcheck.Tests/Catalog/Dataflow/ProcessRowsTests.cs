using Qlcheck.Languages.CSharp.Catalog;
using Qlcheck.Languages.CSharp.Catalog.Dataflow;

namespace Qlcheck.Tests;

// The three process rows split one call between them: the program is command-injection, its
// arguments are command-injection when the program is a shell and command-argument-injection
// otherwise, and each ArgumentList entry is argument-injection.
public class ProcessRowsTests
{
    private const string Command = "command-injection";

    private const string Arguments = "command-argument-injection";

    private const string ArgumentList = "argument-injection";

    [Theory]
    [InlineData("Process.Start(\"git\", args);", Arguments)]
    [InlineData("Process.Start(\"sh\", \"-c \" + args);", Command)]
    [InlineData("var info = new ProcessStartInfo(\"git\", args);", Arguments)]
    [InlineData("var info = new ProcessStartInfo { FileName = \"git\", Arguments = args };", Arguments)]
    [InlineData("var info = new ProcessStartInfo { FileName = \"bash\", Arguments = args };", Command)]
    [InlineData("var info = new ProcessStartInfo(); info.Arguments = args;", Arguments)]
    [InlineData("var info = new ProcessStartInfo(\"git\"); info.ArgumentList.Add(args);", ArgumentList)]
    [InlineData("var info = new ProcessStartInfo(\"git\"); info.ArgumentList.Insert(0, args);", ArgumentList)]
    [InlineData("Process.Start(\"git\", new[] { \"log\", args });", ArgumentList)]
    public void Each_part_of_a_process_start_belongs_to_one_row(string body, string row)
    {
        var findings = Run($"public class R {{ public void M() {{ var args = Console.ReadLine() ?? \"\"; {body} }} }}");

        Assert.Equal([row], findings);
    }

    [Fact]
    public void Adding_to_another_collection_is_not_an_argument()
    {
        Assert.Empty(Run("public class R { public void M() { var list = new System.Collections.ObjectModel.Collection<string>(); list.Add(Console.ReadLine() ?? \"\"); } }"));
    }

    private static List<string> Run(string source)
    {
        string[] ids = [Command, Arguments, ArgumentList];
        var file = new SourceFile("Repo.cs", $"using System; using System.Diagnostics;\n{source}");
        var tree = CSharpTrees.Parse(file);
        var model = CSharpCompilations.Create("process", [tree]).GetSemanticModel(tree);
        var ctx = WalkContext.Create(file, tree, model, ids.ToDictionary(id => id, id => id));
        foreach (var id in ids)
        {
            TaintFlowMatch.Report(ctx, id, TaintEngine.Rows.Single(row => row.Id == id).Taint!);
        }

        return ctx.Findings.Select(finding => finding.Check).ToList();
    }
}

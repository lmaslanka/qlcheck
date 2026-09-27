using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class WalkContextTests
{
    [Fact]
    public void Message_returns_the_message_for_an_id()
    {
        var file = new SourceFile("Repo.cs", "class C { }");
        var tree = CSharpTrees.Parse(file);
        var compilation = CSharpCompilations.Create("fixture", [tree]);
        var model = compilation.GetSemanticModel(tree);
        var ctx = WalkContext.Create(file, tree, model, new Dictionary<string, string> { ["id"] = "hello" });

        Assert.Equal("hello", ctx.Message("id"));
    }
}

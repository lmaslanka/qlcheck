using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

internal static class MatchFixtures
{
    public const string Id = "test-id";

    public static WalkContext Context(string source, string id = Id, string path = "Repo.cs")
    {
        var file = new SourceFile(path, source);
        var tree = CSharpTrees.Parse(file);
        var compilation = CSharpCompilations.Create("fixture", [tree]);
        var model = compilation.GetSemanticModel(tree);
        var messages = new Dictionary<string, string>(StringComparer.Ordinal) { [id] = "message" };
        return WalkContext.Create(file, tree, model, messages);
    }
}

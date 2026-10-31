using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Dataflow;

namespace Qlcheck.Tests;

public class BodyGraphsTests
{
    private const string Sink = "Sink";

    private const string Members = """
        public class C
        {
            public C() { Sink(); }
            ~C() { Sink(); }
            public void Block() { Sink(); }
            public int Arrow() => Sink();
            public static C operator +(C a, C b) { Sink(); return a; }
            public static implicit operator int(C c) => Sink();
            public int Property { get { return Sink(); } set { Sink(); } }
            public int Expression => Sink();
            public int this[int i] => Sink();
            public void Outer() { int Local() => Sink(); System.Func<int> f = () => Sink(); Local(); f(); }
            static int Sink() => 0;
        }

        public abstract class D
        {
            public abstract void NoBody();
            public int Auto { get; set; }
        }
        """;

    private static readonly HashSet<string> SinkNames = new(StringComparer.Ordinal) { Sink };

    [Fact]
    public void Every_executable_body_gets_one_graph_with_its_owner()
    {
        var graphs = Graphs(Members, SinkNames);

        Assert.Equal(
            [".ctor", "Finalize", "Block", "Arrow", "op_Addition", "op_Implicit", "get_Property", "set_Property", "get_Expression", "get_Item", "Outer"],
            graphs.Select(graph => graph.Owner!.Name));
    }

    [Fact]
    public void Local_functions_and_lambdas_are_sub_graphs_of_their_container()
    {
        var outer = Graphs(Members, SinkNames).Single(graph => graph.Owner!.Name == "Outer");

        Assert.Single(outer.Graph.LocalFunctions);
    }

    [Fact]
    public void A_body_that_does_not_mention_a_name_is_skipped()
    {
        const string source = "public class C { public void Quiet() { var x = 1; } public void Loud() { Sink(); } static void Sink() { } }";

        var graph = Assert.Single(Graphs(source, SinkNames));

        Assert.Equal("Loud", graph.Owner!.Name);
    }

    [Fact]
    public void Top_level_statements_are_one_body_owned_by_the_entry_point()
    {
        const string source = "Sink();\nstatic void Sink() { }\nclass Other { void M() { } }";

        var graph = Assert.Single(Graphs(source, SinkNames), graph => graph.Declaration is CompilationUnitSyntax);

        Assert.False(graph.Owner!.CanBeReferencedByName);
    }

    private static List<BodyGraph> Graphs(string source, IReadOnlySet<string> names)
    {
        var tree = CSharpTrees.Parse(new SourceFile("Repo.cs", source));
        var model = CSharpCompilations.Create("graphs", [tree]).GetSemanticModel(tree);
        return new BodyGraphs(tree, model).Containing(token => names.Contains(token.ValueText)).ToList();
    }
}

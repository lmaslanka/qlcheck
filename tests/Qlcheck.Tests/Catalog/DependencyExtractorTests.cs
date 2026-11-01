using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class DependencyExtractorTests
{
    private const string Process = "System.Diagnostics.Process";

    private const string StringBuilder = "System.Text.StringBuilder";

    [Fact]
    public void A_fully_qualified_reference_without_a_using_is_found()
    {
        Assert.Contains(("App.A", Process), Run("namespace App { class A { void M() { var p = System.Diagnostics.Process.GetCurrentProcess(); } } }"));
    }

    [Fact]
    public void A_type_from_the_parent_namespace_is_found()
    {
        Assert.Contains(("Q.Scan.S", "Q.SourceFile"), Run("namespace Q { public class SourceFile { } } namespace Q.Scan { class S { SourceFile f = new(); } }"));
    }

    [Theory]
    [InlineData("using SB = System.Text.StringBuilder; namespace A { class C { SB b = new(); } }")]
    [InlineData("namespace A { class C { StringBuilder b = new(); } }")]
    public void Alias_and_global_usings_resolve_to_the_type(string source)
    {
        Assert.Contains(("A.C", StringBuilder), Run("global using System.Text;", source));
    }

    [Fact]
    public void A_static_using_resolves_to_its_type()
    {
        Assert.Contains(("A.C", "System.Math"), Run("using static System.Math; namespace A { class C { int M() => Max(1, 2); } }"));
    }

    [Fact]
    public void An_extension_method_resolves_to_the_class_that_declares_it()
    {
        var found = Run("namespace E { public static class Ext { public static int Twice(this int x) => x * 2; } } namespace A { using E; class C { int M() => 3.Twice(); } }");

        Assert.Contains(("A.C", "E.Ext"), found);
    }

    [Fact]
    public void Attributes_generic_arguments_base_types_and_nameof_are_found()
    {
        var found = Run("namespace A { class C : System.Exception { System.Collections.Generic.List<System.Text.StringBuilder> l = new(); [System.Obsolete] void M() { var n = nameof(System.IO.File); } } }");

        Assert.Contains(("A.C", "System.Exception"), found);
        Assert.Contains(("A.C", StringBuilder), found);
        Assert.Contains(("A.C", "System.Collections.Generic.List"), found);
        Assert.Contains(("A.C", "System.ObsoleteAttribute"), found);
        Assert.Contains(("A.C", "System.IO.File"), found);
    }

    [Fact]
    public void A_cref_and_an_unresolved_name_give_nothing()
    {
        var found = Run("namespace A { /// <see cref=\"System.IO.File\"/>\nclass C { Missing m; } }");

        Assert.DoesNotContain(found, dependency => dependency.Target == "System.IO.File" || dependency.Target.Contains("Missing", StringComparison.Ordinal));
    }

    [Fact]
    public void A_using_directive_counts_for_every_namespace_the_file_declares()
    {
        Assert.Contains(("Q.Report", "Q.Run"), Run("using Q.Run; namespace Q.Report { class R { } }"));
    }

    [Fact]
    public void One_dependency_per_line_and_target()
    {
        var found = Run("namespace App { class A { void M() { System.Diagnostics.Process.Start(\"x\").Kill(); } } }");

        Assert.Single(found, dependency => dependency.Target == Process);
    }

    private static List<(string Source, string Target)> Run(params string[] sources)
    {
        var trees = sources.Select((source, index) => CSharpTrees.Parse(new SourceFile($"F{index}.cs", source))).ToList();
        var compilation = CSharpCompilations.Create("deps", trees);
        return [.. trees.SelectMany(tree => DependencyExtractor.Extract(compilation.GetSemanticModel(tree), tree.FilePath)).Select(dependency => (dependency.Source, dependency.Target))];
    }
}

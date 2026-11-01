using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Tests;

public class NeverNullTests
{
    private const string Declared = "Declared";

    private const string NewOrEmpty = "ReturnsNewOrEmpty";

    private const string Iterator = "Iterator";

    [Theory]
    [InlineData("#nullable enable\nclass C { System.Collections.Generic.List<int> Get() => throw null!; void M() { var x = Get(); } }", Declared)]
    [InlineData("#nullable enable\nclass C { System.Collections.Generic.List<int>? Get() => new(); void M() { var x = Get(); } }", null)]
    [InlineData("#nullable enable\nclass C { [return: System.Diagnostics.CodeAnalysis.MaybeNull] string Get() => \"\"; void M() { var x = Get(); } }", null)]
    [InlineData("#nullable disable\nclass C { void M() { var x = \"a,b\".Split(','); } }", Declared)]
    [InlineData("#nullable enable\nclass C { T Get<T>() => default!; void M() { var x = Get<string>(); } }", Declared)]
    [InlineData("#nullable enable\nclass C { async System.Threading.Tasks.Task<System.Collections.Generic.List<int>> Get() => new(); async System.Threading.Tasks.Task M() { var x = await Get(); } }", Declared)]
    public void Annotations_decide_first_and_a_question_mark_wins(string source, string? reason)
    {
        Assert.Equal(reason, Reason(source));
    }

    [Theory]
    [InlineData("static System.Collections.Generic.List<int> Get() => new();")]
    [InlineData("private System.Collections.Generic.List<int> Get() { return new System.Collections.Generic.List<int>(); }")]
    [InlineData("static int[] Get() => [];")]
    [InlineData("static int[] Get() => System.Array.Empty<int>();")]
    [InlineData("static System.Collections.Generic.IEnumerable<int> Get() => System.Linq.Enumerable.Empty<int>();")]
    [InlineData("static string Get() => string.Empty;")]
    [InlineData("static string Get() => \"x\";")]
    [InlineData("static string Get(int n) => $\"n = {n}\";")]
    [InlineData("C Get() { return this; }")]
    [InlineData("static string Get(string s) => s ?? \"none\";")]
    [InlineData("static object Get(bool b) => b ? new object() : new System.Text.StringBuilder();")]
    [InlineData("static System.Collections.Generic.List<int> Get() { var list = new System.Collections.Generic.List<int>(); list.Add(1); return list; }")]
    [InlineData("static System.Collections.Generic.List<int> Get() => Make(); static System.Collections.Generic.List<int> Make() => new();")]
    [InlineData("static string Get(string s) => s ?? throw new System.ArgumentNullException(nameof(s));")]
    public void An_unannotated_non_overridable_callee_returning_new_or_empty_values_is_never_null(string member)
    {
        Assert.Equal(NewOrEmpty, Reason($"#nullable disable\nclass C {{ {member} void M() {{ var x = Get({Arguments(member)}); }} }}"));
    }

    [Theory]
    [InlineData("static string Get() => null;")]
    [InlineData("static string Get() { return default; }")]
    [InlineData("static string Get(string s) => s;")]
    [InlineData("static string Get(bool b) { if (b) { return \"x\"; } return null; }")]
    [InlineData("public virtual string Get() => \"x\";")]
    [InlineData("public string Get() => Other(); static string Other() => null;")]
    public void A_null_default_or_unknown_return_or_an_overridable_callee_disqualifies(string member)
    {
        Assert.Null(Reason($"#nullable disable\nclass C {{ {member} void M() {{ var x = Get({Arguments(member)}); }} }}"));
    }

    [Fact]
    public void An_unannotated_iterator_is_never_null()
    {
        Assert.Equal(Iterator, Reason("#nullable disable\nclass C { static System.Collections.Generic.IEnumerable<int> Get() { yield return 1; } void M() { var x = Get(); } }"));
    }

    [Theory]
    [InlineData("#nullable disable\ninterface I { string Get(); } class C { void M(I i) { var x = i.Get(); } }")]
    [InlineData("#nullable disable\nabstract class B { public abstract string Get(); } class C { void M(B b) { var x = b.Get(); } }")]
    public void Interface_and_abstract_callees_are_unknown(string source)
    {
        Assert.Null(Reason(source));
    }

    [Fact]
    public void Mutual_recursion_ends_and_the_depth_limit_holds()
    {
        Assert.Null(Reason("#nullable disable\nclass C { static string A() => B(); static string B() => A(); void M() { var x = A(); } }"));
        Assert.Null(Reason("#nullable disable\nclass C { static string A() => B(); static string B() => D(); static string D() => E(); static string E() => \"x\"; void M() { var x = A(); } }"));
        Assert.Equal(NewOrEmpty, Reason("#nullable disable\nclass C { static string A() => B(); static string B() => D(); static string D() => \"x\"; void M() { var x = A(); } }"));
    }

    // The arguments a test member's Get needs.
    private static string Arguments(string member) =>
        new[] { ("Get(string s)", "\"s\""), ("Get(bool b)", "true"), ("Get(int n)", "1") }
            .FirstOrDefault(entry => member.Contains(entry.Item1, StringComparison.Ordinal)).Item2 ?? string.Empty;

    // The reason for the last call in M (`var x = <call>;`).
    private static string? Reason(string source)
    {
        var tree = CSharpTrees.Parse(new SourceFile("Repo.cs", source));
        var model = CSharpCompilations.Create("nevernull", [tree]).GetSemanticModel(tree);
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(declaration => declaration.Identifier.ValueText == "M");
        var call = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Last();
        return new NeverNull(model).Reason(call)?.Kind.ToString();
    }
}

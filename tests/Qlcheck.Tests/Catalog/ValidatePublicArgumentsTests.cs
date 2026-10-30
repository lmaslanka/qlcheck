using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

// Sources start with `#nullable disable`: the fixture compilation enables nullable, and this Check
// covers only code where nullable annotations are off.
public class ValidatePublicArgumentsTests
{
    private const string Id = "validate-public-arguments";

    [Theory]
    [InlineData("public int Len(string s) => s.Length;")]
    [InlineData("public int Count(System.Collections.Generic.IEnumerable<int> items) { var n = 0; foreach (var x in items) { n++; } return n; }")]
    [InlineData("public char First(string s) => s[0];")]
    [InlineData("public void Sync(object gate) { lock (gate) { } }")]
    [InlineData("public C(string s) { Length = s.Length; } public int Length { get; }")]
    [InlineData("protected int Len(string s) => s.Length;")]
    public void Reports_an_unvalidated_dereference_in_a_public_member(string member)
    {
        var finding = Assert.Single(Run($"public class C {{ {member} }}"));

        Assert.Equal(Id, finding.Check);
    }

    [Fact]
    public void Reports_the_this_parameter_of_a_public_extension_method()
    {
        Assert.Single(Run("public static class E { public static int Len(this string s) => s.Length; }"));
    }

    [Fact]
    public void Reports_at_the_first_dereference_only()
    {
        const string source = "public class C { public int M(string s) { var a = s.Length; return a + s.Length; } }";

        var finding = Assert.Single(Run(source));

        Assert.Equal(source.IndexOf("s.Length", StringComparison.Ordinal) + 1, finding.Column);
    }

    [Theory]
    [InlineData("public int Len(string s) { System.ArgumentNullException.ThrowIfNull(s); return s.Length; }")]
    [InlineData("public int Len(string s) { if (s == null) throw new System.ArgumentNullException(nameof(s)); return s.Length; }")]
    [InlineData("public int Len(string s) { if (s is null) { return 0; } return s.Length; }")]
    [InlineData("public int Len(string s) { if (ReferenceEquals(s, null)) { return 0; } return s.Length; }")]
    [InlineData("public int Len(string s) { var t = s ?? throw new System.ArgumentNullException(nameof(s)); return t.Length + s.Length; }")]
    [InlineData("public int? Len(string s) => s?.Length;")]
    [InlineData("public string Name(string s) => nameof(s) + Other(s);")]
    [InlineData("internal int Len(string s) => s.Length;")]
    [InlineData("private int Len(string s) => s.Length;")]
    [InlineData("public int Len(string s = null) => s.Length;")]
    [InlineData("public int Len(int n) => n.GetHashCode();")]
    [InlineData("public bool Has(string s) => s.IsBlank();")]
    [InlineData("public override string ToString() => base.ToString(); public override bool Equals(object obj) => obj.GetHashCode() == 0; public override int GetHashCode() => 0;")]
    public void Stays_quiet_when_validated_not_dereferenced_or_out_of_scope(string member)
    {
        Assert.Empty(Run($"public class C {{ {member} string Other(string s) => s; }} static class X {{ public static bool IsBlank(this string s) => s == null; }}"));
    }

    [Fact]
    public void Stays_quiet_in_a_type_that_is_not_visible_outside_the_assembly()
    {
        Assert.Empty(Run("class C { public int Len(string s) => s.Length; }"));
        Assert.Empty(Run("public class Outer { private class Inner { public int Len(string s) => s.Length; } }"));
    }

    [Fact]
    public void Stays_quiet_where_nullable_annotations_are_on()
    {
        Assert.Empty(Run("#nullable enable\npublic class C { public int Len(string s) => s.Length; }"));
    }

    [Fact]
    public void Stays_quiet_in_a_file_with_compile_errors()
    {
        Assert.Empty(Run("public class C { public int Len(string s) => s.Length; Missing m; }", allowErrors: true));
    }

    [Fact]
    public void Stays_quiet_on_test_methods()
    {
        Assert.Empty(Run("public class FactAttribute : System.Attribute { } public class T { [Fact] public void Works(string s) { var n = s.Length; } }"));
    }

    // Every source must compile, so a quiet result is never just a broken fixture.
    private static IReadOnlyList<Finding> Run(string source, bool allowErrors = false)
    {
        var text = source.StartsWith("#nullable", StringComparison.Ordinal) ? source : $"#nullable disable\n{source}";
        var ctx = MatchFixtures.Context(text, Id);
        var errors = ctx.Model.GetDiagnostics().Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        Assert.True(allowErrors || !errors.Any(), string.Join("\n", errors));
        PublicArgumentPattern.Apply(ctx, Id);
        return ctx.Findings;
    }
}

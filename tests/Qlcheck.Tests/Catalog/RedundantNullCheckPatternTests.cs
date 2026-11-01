using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class RedundantNullCheckPatternTests
{
    private const string Id = "redundant-null-check";

    private const string Members = """
        static System.Collections.Generic.List<int> Get() => new();
        static async System.Threading.Tasks.Task<System.Collections.Generic.List<int>> GetAsync() => new();
        System.Collections.Generic.List<int> _field = new();
        System.Collections.Generic.List<int> Prop { get; } = new();
        """;

    [Theory]
    [InlineData("if (Get() == null) { }", "Get() == null")]
    [InlineData("if (Get() != null) { }", "Get() != null")]
    [InlineData("if (null == Get()) { }", "null == Get()")]
    [InlineData("if (Get() is null) { }", "Get() is null")]
    [InlineData("if (Get() is not null) { }", "Get() is not null")]
    [InlineData("var n = Get()?.Count;", "?.")]
    [InlineData("var list = Get() ?? new System.Collections.Generic.List<int>();", "Get() ?? new System.Collections.Generic.List<int>()")]
    [InlineData("var list = Get() ?? throw new System.InvalidOperationException();", "Get() ?? throw new System.InvalidOperationException()")]
    [InlineData("var items = Get(); if (items == null) { return; }", "items == null")]
    public void Each_counted_shape_on_a_never_null_result_is_one_Finding(string body, string at)
    {
        var finding = Assert.Single(Run($"void M() {{ {body} }}"));

        Assert.Equal(Id, finding.Check);
        Assert.Equal(Column(body, at), finding.Column);
        Assert.Null(finding.Replacement);
    }

    [Fact]
    public void An_awaited_result_is_checked_too()
    {
        Assert.Single(Run("async System.Threading.Tasks.Task M() { var items = await GetAsync(); if (items is null) { return; } }"));
    }

    [Theory]
    [InlineData("if (Get() is { }) { }")]
    [InlineData("if (Get() is System.Collections.Generic.List<int> list) { }")]
    [InlineData("System.ArgumentNullException.ThrowIfNull(Get());")]
    [InlineData("var empty = string.IsNullOrEmpty(Get().ToString());")]
    [InlineData("var same = ReferenceEquals(Get(), null);")]
    public void Other_shapes_are_not_counted(string body)
    {
        Assert.Empty(Run($"void M() {{ {body} }}"));
    }

    [Theory]
    [InlineData("void M() { var items = Get(); items = Maybe(); if (items == null) { } } static System.Collections.Generic.List<int> Maybe() => null;")]
    [InlineData("void M() { var items = Get(); Fill(ref items); if (items == null) { } } static void Fill(ref System.Collections.Generic.List<int> l) { }")]
    [InlineData("void M(System.Collections.Generic.List<int> items) { if (items == null) { } }")]
    [InlineData("void M() { if (_field == null) { } if (Prop == null) { } }")]
    [InlineData("void M() { if (Maybe() == null) { } } static System.Collections.Generic.List<int> Maybe() => null;")]
    public void Reassigned_locals_parameters_fields_properties_and_nullable_results_give_nothing(string members)
    {
        Assert.Empty(Run(members));
    }

    [Fact]
    public void A_file_with_compile_errors_gives_nothing()
    {
        Assert.Empty(Run("void M() { if (Get() == null) { } Missing m; }", allowErrors: true));
    }

    [Theory]
    [InlineData("#nullable enable\npublic class C { System.Collections.Generic.List<int> Items() => new(); void M() { if (Items() == null) { } } }", "`Items` is declared to return a non-nullable `List<int>`.")]
    [InlineData("#nullable disable\npublic class C { static System.Collections.Generic.List<int> Items() => new(); void M() { if (Items() == null) { } } }", "`Items` returns a new or empty value on every path.")]
    [InlineData("#nullable disable\npublic class C { static System.Collections.Generic.IEnumerable<int> Lines() { yield return 1; } void M() { if (Lines() == null) { } } }", "`Lines` is an iterator, which never returns null.")]
    public void The_message_names_the_method_and_why(string source, string detail)
    {
        var finding = Assert.Single(RunSource(source));

        Assert.Equal($"message {detail}", finding.Message);
    }

    private static int Column(string body, string at)
    {
        var line = $"#nullable disable\npublic class C {{ {Members.ReplaceLineEndings(" ")} void M() {{ {body} }} }}".Split('\n')[1];
        return line.IndexOf(at, StringComparison.Ordinal) + 1;
    }

    private static IReadOnlyList<Finding> Run(string members, bool allowErrors = false) =>
        RunSource($"#nullable disable\npublic class C {{ {Members.ReplaceLineEndings(" ")} {members} }}", allowErrors);

    private static IReadOnlyList<Finding> RunSource(string source, bool allowErrors = false)
    {
        var ctx = MatchFixtures.Context(source, Id);
        Assert.True(allowErrors || !ctx.Model.GetDiagnostics().Any(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
        RedundantNullCheckPattern.Apply(ctx, Id);
        return ctx.Findings;
    }
}

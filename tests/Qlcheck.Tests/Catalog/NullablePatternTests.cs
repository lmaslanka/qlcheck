using Microsoft.CodeAnalysis;
using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public sealed class NullablePatternTests : IDisposable
{
    private const string Argument = "nullable-argument";

    private const string Return = "nullable-return";

    private const string Assignment = "nullable-assignment";

    private const string Uninitialized = "non-nullable-uninitialized";

    private const string Dereference = "null-dereference";

    private const string Enabled = "<PropertyGroup><Nullable>enable</Nullable></PropertyGroup>";

    private const string PassesNullable = "class C { int M(string? s) => N(s); int N(string t) => t.Length; }";

    private static readonly string[] Ids = [Argument, Return, Assignment, Uninitialized, Dereference];

    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("qlcheck_nullable_");

    public void Dispose() => _dir.Delete(recursive: true);

    [Theory]
    [InlineData(PassesNullable, Argument)]
    [InlineData("class C { string M(string? s) { return s; } }", Return)]
    [InlineData("class C { void M(string? s) { string t = s; } }", Assignment)]
    [InlineData("class C { string? f; void M() { string t = null; } }", Assignment)]
    [InlineData("class C { string Name; }", Uninitialized)]
    public void Each_compiler_warning_goes_to_its_row(string source, string id)
    {
        var finding = Assert.Single(Run(source));

        Assert.Equal(id, finding.Check);
    }

    [Fact]
    public void A_possibly_null_value_that_is_dereferenced_gives_null_dereference()
    {
        const string source = """
            class C
            {
                int M()
                {
                    string? s = System.Environment.GetEnvironmentVariable("X");
                    return s.Length;
                }
            }
            """;

        Assert.Equal(Dereference, Assert.Single(Run(source, Project(Enabled))).Check);
    }

    [Theory]
    [InlineData("class C { int M(string? s) { s?.Trim(); return s.Length; } }", 1)]
    [InlineData("class C { int M(System.Collections.Generic.List<int>? x) { if (x == null) { x = new(); return x.Count; } return 0; } }", 0)]
    [InlineData("class C { int M(string? s) { if (s == null) { return 0; } return s.Length; } }", 0)]
    public void Null_dereference_follows_the_compilers_flow_analysis(string source, int expected)
    {
        Assert.Equal(expected, Run(source).Count(finding => finding.Check == Dereference));
    }

    [Fact]
    public void The_message_ends_with_the_compiler_id_and_text()
    {
        var finding = Assert.Single(Run(PassesNullable));

        Assert.StartsWith(Catalog.Messages[Argument], finding.Message, StringComparison.Ordinal);
        Assert.Matches(@" \(CS8604: Possible null reference argument for parameter 't' in .*\.\)$", finding.Message);
    }

    [Fact]
    public void Two_uninitialized_members_reported_at_one_constructor_give_one_finding_naming_both()
    {
        var finding = Assert.Single(Run("class C { string a; string b; public C() { } }"));

        Assert.Contains("'a'", finding.Message, StringComparison.Ordinal);
        Assert.Contains("'b'", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("#nullable disable\nclass C { int M(string? s) => N(s); int N(string t) => t.Length; }")]
    [InlineData("class C { int M(string? s) =>\n#pragma warning disable CS8604\nN(s);\n#pragma warning restore CS8604\nint N(string t) => t.Length; }")]
    [InlineData("class C { Missing m; int M(string? s) => N(s); int N(string t) => t.Length; }")]
    [InlineData("partial class C { string Name; }")]
    public void Disabled_regions_suppressed_warnings_broken_trees_and_partial_types_give_nothing(string source)
    {
        Assert.Empty(Run(source));
    }

    [Fact]
    public void A_project_follows_its_nullable_setting()
    {
        Assert.Single(Run(PassesNullable, Project(Enabled)));
        Assert.Empty(Run(PassesNullable, Project("<PropertyGroup><Nullable>disable</Nullable></PropertyGroup>")));
    }

    [Fact]
    public void A_project_that_targets_only_unannotated_frameworks_gives_nothing()
    {
        const string body = "<PropertyGroup><Nullable>enable</Nullable><TargetFrameworks>netstandard2.0;net48</TargetFrameworks></PropertyGroup>";

        Assert.Empty(Run(PassesNullable, Project(body)));
    }

    [Fact]
    public void A_warning_in_a_context_tree_is_never_reported()
    {
        var csproj = Project(Enabled);
        var obj = Directory.CreateDirectory(Path.Combine(_dir.FullName, "obj/Debug/net10.0"));
        File.WriteAllText(Path.Combine(obj.FullName, "Sample.GlobalUsings.g.cs"), "global using System;\nclass Hidden { string Name; }\n");

        Assert.Empty(Run("class C { }", csproj));
    }

    private string Project(string body)
    {
        var csproj = Path.Combine(_dir.FullName, "Sample.csproj");
        File.WriteAllText(csproj, $"<Project Sdk=\"Microsoft.NET.Sdk\">{body}</Project>");
        return csproj;
    }

    private IReadOnlyList<Finding> Run(string source, string? csproj = null)
    {
        var fullPath = Path.Combine(_dir.FullName, "Repo.cs");
        var file = new SourceFile("Repo.cs", source);
        var trees = new Dictionary<string, SyntaxTree> { [fullPath] = CSharpTrees.Parse(file) };
        var checks = Catalog.Checks.Where(check => Ids.Contains(check.Id)).ToList();
        return CatalogRun.Execute([new SourceScan.LoadedSource(fullPath, file)], csproj, trees, checks);
    }
}

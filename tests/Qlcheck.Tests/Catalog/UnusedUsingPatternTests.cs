using Microsoft.CodeAnalysis;
using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class UnusedUsingPatternTests
{
    private const string CompilationName = "qlcheck";

    private const string Id = "unused-using";

    private const string Message = "message";

    private static IReadOnlyList<Finding> Run(string source, string path = "Repo.cs") =>
        RunWithReportable(source, path, reportable: true);

    private static IReadOnlyList<Finding> RunWithReportable(string source, string path, bool reportable)
    {
        var tree = CSharpTrees.Parse(new SourceFile(path, source));
        var compilation = CSharpCompilations.Create(CompilationName, [tree]);
        var messages = new Dictionary<string, string>(StringComparer.Ordinal) { [Id] = Message };
        var trees = reportable ? new HashSet<SyntaxTree> { tree } : [];
        var ctx = new CompilationContext(compilation, trees, messages);
        UnusedUsingPattern.Apply(ctx, Id);
        return ctx.Findings;
    }

    [Fact]
    public void Skips_a_tree_that_uses_an_inaccessible_type()
    {
        var library = CSharpCompilations.Create("library", [CSharpTrees.Parse(new SourceFile("Lib.cs", "namespace Lib { internal class Hidden { } }"))]);
        var tree = CSharpTrees.Parse(new SourceFile("Repo.cs", "using Lib;\n\nclass C\n{\n    Hidden field;\n}\n"));
        var compilation = CSharpCompilations.Create(CompilationName, [tree]).AddReferences(library.ToMetadataReference());
        var ctx = new CompilationContext(compilation, new HashSet<SyntaxTree> { tree }, new Dictionary<string, string>(StringComparer.Ordinal) { [Id] = Message });

        UnusedUsingPattern.Apply(ctx, Id);

        Assert.Contains(compilation.GetDiagnostics(), diagnostic => diagnostic.Id == "CS0122");
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void Reports_unused_using()
    {
        var source = """
            using System.Text;

            class C
            {
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal(Id, finding.Check);
        Assert.Equal(string.Empty, finding.Replacement);
    }

    [Fact]
    public void Ignores_used_using()
    {
        var source = """
            using System.Text;

            class C
            {
                StringBuilder M() => new StringBuilder();
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Ignores_using_whose_namespace_is_not_in_the_compilation()
    {
        var source = """
            using Missing.Package;

            class C
            {
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Ignores_using_that_is_used_when_types_are_not_in_the_compilation()
    {
        var source = """
            namespace Tests;

            using App.Status;

            class C
            {
                void M() => Classifier.Run();
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Ignores_finding_in_a_tree_outside_the_reportable_set()
    {
        var source = """
            using System.Text;

            class C
            {
            }
            """;

        Assert.Empty(RunWithReportable(source, "Repo.cs", reportable: false));
    }

    [Fact]
    public void Ignores_using_when_file_has_unresolved_types()
    {
        var source = """
            using System.Net.Http;

            class C
            {
                IHttpClientFactory M() => null!;
            }
            """;

        Assert.Empty(Run(source));
    }
}

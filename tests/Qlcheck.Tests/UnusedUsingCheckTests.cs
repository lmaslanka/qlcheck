namespace Qlcheck.Tests;

public class UnusedUsingCheckTests
{
    private static IReadOnlyList<Finding> Run(string source, string path = "Repo.cs")
    {
        var file = new SourceFile(path, source);
        var compilation = CSharpCompilations.Create("qlcheck", [file.Tree]);
        var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path };
        return new UnusedUsingCheck().AnalyzeCompilation(compilation, included);
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
        Assert.Equal("unused-using", finding.Check);
        Assert.Equal(UnusedUsingCheck.Message, finding.Message);
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

namespace Qlcheck.Tests;

public class UnusedUsingCheckTests
{
    private static IReadOnlyList<Finding> Run(string source, string path = "Repo.cs")
    {
        ICheck check = new UnusedUsingCheck();
        return check.Run(new CheckContext([new SourceFile(path, source)]));
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
}

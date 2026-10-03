using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class OneTypePerFilePatternTests
{
    private const int ExtraTypeCount = 2;

    private static IReadOnlyList<Finding> Run(string source, string path = "Repo.cs")
    {
        var ctx = MatchFixtures.Context(source, path: path);
        OneTypePerFilePattern.Apply(ctx, MatchFixtures.Id);
        return ctx.Findings;
    }

    [Fact]
    public void Reports_second_top_level_type()
    {
        var source = """
            class A { }
            record B { }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal(MatchFixtures.Id, finding.Check);
        Assert.Equal("Move 'B' into its own file.", finding.Message);
    }

    [Fact]
    public void Ignores_nested_types()
    {
        var source = """
            class A
            {
                class Nested { }
                record Inner { }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Ignores_generated_files()
    {
        var source = """
            class A { }
            record B { }
            """;

        Assert.Empty(Run(source, path: "Repo.g.cs"));
    }

    [Fact]
    public void Ignores_partials_of_the_same_type()
    {
        var source = """
            partial class A { }
            partial class A { }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Reports_struct_interface_and_enum()
    {
        var source = """
            struct A { }
            interface I { }
            enum E { }
            """;

        Assert.Equal(ExtraTypeCount, Run(source).Count);
    }
}

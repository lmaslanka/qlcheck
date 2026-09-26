namespace Qlcheck.Tests;

public class OneTypePerFileCheckTests
{
    private const int ExtraTypeCount = 2;
    private static IReadOnlyList<Finding> Run(string source, string path = "Repo.cs")
    {
        var file = new SourceFile(path, source);
        return new OneTypePerFileCheck().Analyze(file, CSharpTrees.Parse(file));
    }

    [Fact]
    public void Reports_second_top_level_type()
    {
        var source = """
            class A { }
            record B { }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal(OneTypePerFileCheck.CheckId, finding.Check);
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

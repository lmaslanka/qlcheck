namespace Qlcheck.Tests;

public class TanglePatternTests
{
    private const string Tangle = "architecture-tangle";

    // References on one line count once (one Finding per line and target), so the stronger edges
    // put theirs on separate lines.
    [Fact]
    public void A_two_cycle_is_broken_at_its_weaker_edge()
    {
        var findings = Run(
            "namespace Q.A { public class X { Q.B.Y y1 = new();\n Q.B.Y y2 = new(); } }",
            "namespace Q.B { public class Y { Q.A.X x = new(); } }");

        var finding = Assert.Single(findings);
        Assert.Equal("F1.cs", finding.File);
        Assert.Equal("`Q.B` -> `Q.A` closes the cycle Q.B -> Q.A -> Q.B (.qlcheck_architecture:1); it is the weakest link, 1 reference. Remove it, or move what both need into a component neither depends back on.", finding.Message);
    }

    [Fact]
    public void A_three_cycle_reports_its_weakest_edge()
    {
        var findings = Run(
            "namespace Q.A { public class X { Q.B.Y y1 = new();\n Q.B.Y y2 = new(); } }",
            "namespace Q.B { public class Y { Q.C.Z z1 = new();\n Q.C.Z z2 = new(); } }",
            "namespace Q.C { public class Z { Q.A.X x = new(); } }");

        Assert.Equal(["F2.cs"], findings.Select(finding => finding.File));
        Assert.Contains("closes the cycle Q.C -> Q.A -> Q.B -> Q.C", findings[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_overlapping_cycles_are_both_broken()
    {
        var findings = Run(
            "namespace Q.A { public class X { Q.B.Y b1 = new();\n Q.B.Y b2 = new();\n Q.C.Z c1 = new();\n Q.C.Z c2 = new(); } }",
            "namespace Q.B { public class Y { Q.A.X a = new(); } }",
            "namespace Q.C { public class Z { Q.A.X a = new(); } }");

        Assert.Equal(["F1.cs", "F2.cs"], findings.Select(finding => finding.File).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Ties_break_by_name_the_same_way_every_time()
    {
        string[] sources = ["namespace Q.A { public class X { Q.B.Y y = new(); } }", "namespace Q.B { public class Y { Q.A.X x = new(); } }"];

        var first = Run(sources).Single();
        for (var run = 0; run < 10; run++)
        {
            Assert.Equal(first.Id, Run(sources).Single().Id);
        }

        Assert.Equal("F0.cs", first.File);
    }

    [Fact]
    public void Types_directly_in_the_prefix_belong_to_no_component()
    {
        Assert.Empty(Run(
            "namespace Q { public class Kernel { Q.A.X x = new(); } }",
            "namespace Q.A { public class X { Q.Kernel k = new(); } }"));
    }

    private static IReadOnlyList<Finding> Run(params string[] sources) => ArchitectureFixture.Run("no-cycles Q.*", Tangle, sources);
}

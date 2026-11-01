namespace Qlcheck.Tests;

public class ArchitectureEngineTests
{
    private const string Relationship = "architecture-relationship";

    private const int TwoReferences = 2;

    private const string Kernel = "namespace Q { public class SourceFile { } public class Report { } }";

    [Fact]
    public void Must_not_use_reports_a_reference_from_the_source_to_the_target()
    {
        var finding = Assert.Single(Run("Q.Scan must-not-use Q", Kernel, "namespace Q.Scan { class S { SourceFile f = new(); } }"));

        Assert.Equal(Relationship, finding.Check);
        Assert.Equal("`Q.Scan` must not use `Q.SourceFile` (.qlcheck_architecture:1). Move this code to a component that may use it, or move `SourceFile` into one `Q.Scan` may use.", finding.Message);
    }

    [Fact]
    public void Must_not_use_lets_a_source_use_itself()
    {
        Assert.Empty(Run("Q.Scan must-not-use Q", Kernel, "namespace Q.Scan { class S { T t = new(); } class T { } }"));
    }

    [Fact]
    public void Only_used_by_reports_a_forbidden_user_and_spares_allowed_ones_and_itself()
    {
        var findings = Run(
            "Q.Report only-used-by Q.Out",
            Kernel,
            "namespace Q.Out { class Writer { Report r = new(); } }",
            "namespace Q.Core { class Engine { Report r = new(); } }");

        var finding = Assert.Single(findings);
        Assert.Equal("Only `Q.Out` may use `Q.Report` (.qlcheck_architecture:1). Call one of them instead.", finding.Message);
        Assert.Equal("F2.cs", finding.File);
    }

    [Fact]
    public void A_using_directive_is_a_reference_of_its_own()
    {
        var findings = Run("Q.Report must-not-use Q.Run", "namespace Q.Run { public class Job { } }", "using Q.Run; namespace Q.Report { class R { Job j = new(); } }");

        Assert.Equal(TwoReferences, findings.Count);
    }

    [Fact]
    public void Without_a_file_nothing_is_extracted_or_reported()
    {
        Assert.Empty(Run(null, Kernel, "namespace Q.Scan { class S { SourceFile f = new(); } }"));
    }

    // The architecture file has the given single line, or none at all when it is null.
    private static IReadOnlyList<Finding> Run(string? constraint, params string[] sources) => ArchitectureFixture.Run(constraint, Relationship, sources);
}

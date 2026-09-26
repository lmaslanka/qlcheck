namespace Qlcheck.Tests;

public class CoverageMapTests
{
    private const string DisplayPath = "src/Foo.cs";

    private const string TempFolder = "qlcheck-coverage-map";

    private const string MethodName = "M";

    private const int BodyLine = 4;

    private const int ReturnLine = 5;

    private const int CoveredLine = 3;

    private const string MethodSource = "class C\n{\n    int M()\n    {\n        return 1;\n    }\n}\n";

    private const string InterfaceSource = "interface I\n{\n    void M();\n}\n";

    [Fact]
    public void Reports_each_uncovered_line_and_groups_methods()
    {
        var full = Path.Combine(Path.GetTempPath(), TempFolder, "Foo.cs");
        var hits = LcovMap.Parse($"""
            SF:{full}
            FN:{CoveredLine},{MethodName}
            DA:{CoveredLine},1
            DA:{ReturnLine},0
            end_of_record
            """);
        var source = new SourceScan.LoadedSource(full, new SourceFile(DisplayPath, MethodSource));

        var analysis = CoverageMap.Build([source], hits);

        var finding = Assert.Single(analysis.Findings);
        Assert.Equal(CoverageCheck.CheckId, finding.Check);
        Assert.Equal(DisplayPath, finding.File);
        Assert.Equal(ReturnLine, finding.Line);
        Assert.Equal(CoverageCheck.UncoveredMessage, finding.Message);
        var file = Assert.Single(analysis.Files);
        Assert.False(file.Missing);
        var method = Assert.Single(file.Methods);
        Assert.Equal(MethodName, method.Name);
        Assert.Equal([ReturnLine], method.Lines);
    }

    [Fact]
    public void Missing_file_with_a_body_is_not_clean()
    {
        var full = Path.Combine(Path.GetTempPath(), TempFolder, "Missing.cs");
        var source = new SourceScan.LoadedSource(full, new SourceFile(DisplayPath, MethodSource));

        var analysis = CoverageMap.Build([source], LcovMap.Parse(string.Empty));

        var finding = Assert.Single(analysis.Findings);
        Assert.Equal(CoverageCheck.MissingMessage, finding.Message);
        Assert.Equal(BodyLine, finding.Line);
        var file = Assert.Single(analysis.Files);
        Assert.True(file.Missing);
    }

    [Fact]
    public void Missing_file_without_a_body_is_clean()
    {
        var full = Path.Combine(Path.GetTempPath(), TempFolder, "Empty.cs");
        var source = new SourceScan.LoadedSource(full, new SourceFile(DisplayPath, InterfaceSource));

        var analysis = CoverageMap.Build([source], LcovMap.Parse(string.Empty));

        Assert.Empty(analysis.Findings);
        var file = Assert.Single(analysis.Files);
        Assert.False(file.Missing);
        Assert.Empty(file.Methods);
    }
}

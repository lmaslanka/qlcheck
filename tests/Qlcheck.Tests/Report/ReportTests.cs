using ReportWriter = Qlcheck.Report.Report;

namespace Qlcheck.Tests;

public class ReportTests
{
    private static Qlcheck.Cli.Cli.Options Options(bool human = false, bool stats = false) =>
        new(human, stats, false, [], [], []);

    [Fact]
    public void Write_prints_a_blank_line_before_stats_when_human_output_produced_findings()
    {
        var findings = new List<Finding> { Finding.At("check", "Repo.cs", 1, 1, "message") };
        var loaded = new List<SourceScan.LoadedSource> { new("/repo/Repo.cs", new SourceFile("Repo.cs", "class C { }")) };
        var stdout = new StringWriter();

        ReportWriter.Write(Options(human: true, stats: true), loaded, [], findings, [], TimeSpan.Zero, stdout);

        Assert.Contains("\n\n╭", stdout.ToString());
    }

    [Fact]
    public void WriteHuman_prints_the_suggested_replacement()
    {
        var findings = new List<Finding> { new("id", "check", "Repo.cs", 1, 1, "message", "line1\nline2") };
        var stdout = new StringWriter();

        ReportWriter.Write(Options(human: true), [], [], findings, [], TimeSpan.Zero, stdout);

        Assert.Contains("  line1", stdout.ToString());
        Assert.Contains("  line2", stdout.ToString());
    }

    [Fact]
    public void WriteStats_prints_placeholders_when_there_are_no_findings()
    {
        var loaded = new List<SourceScan.LoadedSource> { new("/repo/Repo.cs", new SourceFile("Repo.cs", "class C { }")) };
        var stdout = new StringWriter();

        ReportWriter.Write(Options(stats: true), loaded, [], [], [], TimeSpan.Zero, stdout);

        var text = stdout.ToString();
        Assert.Contains("By check", text);
        Assert.Contains("(none)", text);
    }

    [Fact]
    public void WriteStats_counts_zero_lines_for_an_empty_file()
    {
        var loaded = new List<SourceScan.LoadedSource> { new("/repo/Repo.cs", new SourceFile("Repo.cs", string.Empty)) };
        var stdout = new StringWriter();

        ReportWriter.Write(Options(stats: true), loaded, [], [], [], TimeSpan.Zero, stdout);

        Assert.Contains("Lines checked", stdout.ToString());
    }

    [Fact]
    public void WriteStats_truncates_a_long_check_row_label()
    {
        var longCheck = new string('x', 60);
        var findings = new List<Finding> { Finding.At(longCheck, "Repo.cs", 1, 1, "message") };
        var loaded = new List<SourceScan.LoadedSource> { new("/repo/Repo.cs", new SourceFile("Repo.cs", "class C { }")) };
        var stdout = new StringWriter();

        ReportWriter.Write(Options(stats: true), loaded, [], findings, [], TimeSpan.Zero, stdout);

        Assert.Contains('…', stdout.ToString());
    }

    [Fact]
    public void WriteStats_formats_durations_of_a_second_or_more()
    {
        var loaded = new List<SourceScan.LoadedSource> { new("/repo/Repo.cs", new SourceFile("Repo.cs", "class C { }")) };
        var stdout = new StringWriter();

        ReportWriter.Write(Options(stats: true), loaded, [], [], [], TimeSpan.FromSeconds(2.5), stdout);

        Assert.Contains("2.50 s", stdout.ToString());
    }

    [Fact]
    public void Write_serializes_coverage_with_a_source_snippet()
    {
        var source = "line one\nline two\nline three\n";
        var loaded = new List<SourceScan.LoadedSource> { new("/repo/Repo.cs", new SourceFile("Repo.cs", source)) };
        var coverage = new List<CoverageFile> { new("Repo.cs", false, [new CoverageMethod("M", [2])]) };
        var stdout = new StringWriter();

        ReportWriter.Write(Options(), loaded, [], [], coverage, TimeSpan.Zero, stdout);

        Assert.Contains("line two", stdout.ToString());
    }

    [Fact]
    public void Write_serializes_coverage_for_a_file_with_no_matching_source_text()
    {
        var coverage = new List<CoverageFile> { new("Missing.cs", false, [new CoverageMethod("M", [1])]) };
        var stdout = new StringWriter();

        ReportWriter.Write(Options(), [], [], [], coverage, TimeSpan.Zero, stdout);

        Assert.Contains("\"text\": \"1|\"", stdout.ToString());
    }

    [Fact]
    public void Write_serializes_coverage_for_the_last_line_without_a_trailing_newline()
    {
        var source = "line one\nline two";
        var loaded = new List<SourceScan.LoadedSource> { new("/repo/Repo.cs", new SourceFile("Repo.cs", source)) };
        var coverage = new List<CoverageFile> { new("Repo.cs", false, [new CoverageMethod("M", [2])]) };
        var stdout = new StringWriter();

        ReportWriter.Write(Options(), loaded, [], [], coverage, TimeSpan.Zero, stdout);

        Assert.Contains("line two", stdout.ToString());
    }
}

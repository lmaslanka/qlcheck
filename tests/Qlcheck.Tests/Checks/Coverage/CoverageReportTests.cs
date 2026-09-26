namespace Qlcheck.Tests;

public class CoverageReportTests
{
    private const string DisplayPath = "src/Foo.cs";

    private const string FullPath = "/tmp/Foo.cs";

    private const string CoverageProperty = "\"coverage\"";

    private const string MethodsProperty = "\"methods\"";

    private const string MissingProperty = "\"missing\"";

    private const string MethodName = "M";

    private const int ReturnLine = 5;

    private const string Source = "class C\n{\n    int M()\n    {\n        return 1;\n    }\n}\n";

    [Fact]
    public void Json_groups_uncovered_lines_for_an_llm()
    {
        var loaded = new SourceScan.LoadedSource(FullPath, new SourceFile(DisplayPath, Source));
        var finding = Finding.At(CoverageCheck.CheckId, DisplayPath, ReturnLine, 1, CoverageCheck.UncoveredMessage);
        var coverage = new CoverageFile(DisplayPath, false, [new CoverageMethod(MethodName, [ReturnLine])]);
        var stdout = new StringWriter();

        Qlcheck.Report.Report.Write(
            new Qlcheck.Cli.Cli.Options(false, false, [], [], [DisplayPath]),
            [loaded],
            [new CoverageCheck()],
            [finding],
            [coverage],
            TimeSpan.Zero,
            stdout);

        var json = stdout.ToString();
        Assert.Contains(CoverageProperty, json);
        Assert.Contains("\"name\": \"M\"", json);
        Assert.Contains("5|        return 1;", json);
        Assert.DoesNotContain(MissingProperty, json);
    }

    [Fact]
    public void Clean_file_is_present_without_methods()
    {
        var loaded = new SourceScan.LoadedSource(FullPath, new SourceFile(DisplayPath, Source));
        var coverage = new CoverageFile(DisplayPath, false, []);
        var stdout = new StringWriter();

        Qlcheck.Report.Report.Write(
            new Qlcheck.Cli.Cli.Options(false, false, [], [], [DisplayPath]),
            [loaded],
            [new CoverageCheck()],
            [],
            [coverage],
            TimeSpan.Zero,
            stdout);

        var json = stdout.ToString();
        Assert.Contains("\"file\": \"src/Foo.cs\"", json);
        Assert.DoesNotContain(MethodsProperty, json);
        Assert.DoesNotContain(MissingProperty, json);
    }

    [Fact]
    public void Human_output_stays_per_line()
    {
        var finding = Finding.At(CoverageCheck.CheckId, DisplayPath, ReturnLine, 1, CoverageCheck.UncoveredMessage);
        var stdout = new StringWriter();

        Qlcheck.Report.Report.Write(
            new Qlcheck.Cli.Cli.Options(true, false, [], [], [DisplayPath]),
            [],
            [new CoverageCheck()],
            [finding],
            [new CoverageFile(DisplayPath, true, [])],
            TimeSpan.Zero,
            stdout);

        var text = stdout.ToString();
        Assert.Contains("src/Foo.cs:5:1  coverage", text);
        Assert.Contains(CoverageCheck.UncoveredMessage, text);
        Assert.DoesNotContain(CoverageProperty, text);
    }
}

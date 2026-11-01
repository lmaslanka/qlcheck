using System.Text.Json;

namespace Qlcheck.Tests;

// architecture-relationship through the whole CLI, with a .qlcheck_architecture at the git root.
public sealed class ArchitectureRunTests : IDisposable
{
    private const string Id = "architecture-relationship";

    private const string Writer = "Writer";

    private const string ArchitectureKey = "architecture";

    private const string Report = "src/Report/Writer.cs";

    private const string Engine = "src/Core/Engine.cs";

    private const string Kernel = "namespace App.Run { public class Job { } }\n";

    private readonly string _dir = Directory.CreateTempSubdirectory("qlcheck_archrun_").FullName;

    public ArchitectureRunTests()
    {
        TestGit.Init(_dir);
        Write(ArchitectureFiles.FileName, "App.Report App.Core must-not-use App.Run\n");
        Write("src/Run/Job.cs", Kernel);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void A_breach_exits_one_and_a_clean_tree_exits_zero()
    {
        Write(Report, User("App.Report", Writer));
        Assert.Equal((ExitCode.Findings, 1), Run(Id));

        Write(Report, $"namespace App.Report {{ class {Writer} {{ }} }}\n");
        Assert.Equal((ExitCode.Clean, 0), Run(Id));
    }

    [Fact]
    public void A_disabled_tree_and_a_suppression_comment_drop_their_Findings()
    {
        Write(Report, User("App.Report", Writer));
        Write("src/Report/.qlcheck_disable", $"{Id}\n");
        Write(Engine, $"namespace App.Core {{ class Engine {{\n// qlcheck-ignore: {Id} checked-on:{TestDates.Today}\nApp.Run.Job j = new(); }} }}\n");

        Assert.Equal((ExitCode.Clean, 0), Run());
    }

    [Fact]
    public void Uncommitted_keeps_only_the_changed_file()
    {
        Write(Report, User("App.Report", Writer));
        TestGit.CommitAll(_dir);
        Write(Engine, User("App.Core", "Engine"));

        Assert.Equal((ExitCode.Findings, 1), Run(Id, "--uncommitted"));
    }

    [Fact]
    public void A_cycle_that_only_two_projects_together_close_is_found()
    {
        const string Project = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n";
        Write(ArchitectureFiles.FileName, "no-cycles Q.*\n");
        Write("one/One.csproj", Project);
        Write("two/Two.csproj", Project);
        Write("one/A.cs", "namespace Q.A { class X { Q.B.Y y = new(); } }\nnamespace Q.B { class Y { } }\n");
        Write("two/B.cs", "namespace Q.B { class Z { Q.A.W w = new(); } }\nnamespace Q.A { class W { } }\n");

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var code = QlcheckApp.Run(["--check", "architecture-tangle", _dir], stdout, stderr);

        Assert.Equal(ExitCode.Findings, code);
        Assert.Contains("closes the cycle", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_applied_file_is_shown_in_json_and_stats_and_absent_without_one()
    {
        Write(Report, User("App.Report", Writer));

        using var json = JsonDocument.Parse(Output(_dir));
        var applied = Assert.Single(json.RootElement.GetProperty(ArchitectureKey).EnumerateArray());
        Assert.EndsWith(ArchitectureFiles.FileName, applied.GetProperty("file").GetString(), StringComparison.Ordinal);
        Assert.Equal(1, applied.GetProperty("constraints").GetInt32());
        Assert.Matches(@"Architecture\s+│", Output("--stats", _dir));

        File.Delete(Path.Combine(_dir, ArchitectureFiles.FileName));
        using var bare = JsonDocument.Parse(Output(_dir));
        Assert.False(bare.RootElement.TryGetProperty(ArchitectureKey, out _));
    }

    private static string Output(params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        QlcheckApp.Run(args, stdout, stderr);
        return stdout.ToString();
    }

    private static string User(string ns, string type) => $"namespace {ns} {{ class {type} {{ App.Run.Job j = new(); }} }}\n";

    private void Write(string path, string text)
    {
        var full = Path.Combine(_dir, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    // The exit code and the number of architecture-relationship Findings. With `only`, that
    // Check runs (and disable files are ignored); without it every Check runs, so other Checks'
    // Findings are not counted and the run counts as clean when no architecture Finding is left.
    private (ExitCode Code, int Findings) Run(string? only = null, string? flag = null)
    {
        string[] args = [.. only is null ? [] : new[] { "--check", only }, .. flag is null ? [] : new[] { flag }, _dir];
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var code = QlcheckApp.Run(args, stdout, stderr);
        using var json = JsonDocument.Parse(stdout.ToString());
        var count = json.RootElement.GetProperty("findings").EnumerateArray().Count(finding => finding.GetProperty("check").GetString() == Id);
        return (only is null && count == 0 ? ExitCode.Clean : code, count);
    }
}

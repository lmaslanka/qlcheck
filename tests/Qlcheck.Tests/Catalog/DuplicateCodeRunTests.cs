using System.Text.Json;

namespace Qlcheck.Tests;

// duplicate-code through the whole CLI: each copy is its own Finding, so --uncommitted, suppression
// comments and disable files apply to each copy alone, and copies are found across projects.
public sealed class DuplicateCodeRunTests : IDisposable
{
    private const string Id = "duplicate-code";

    private const string CopyA = "src/A.cs";

    private const string CopyB = "src/B.cs";

    private const string TestCopy = "tests/B.cs";

    private const string Daily = "Daily";

    private const string Weekly = "Weekly";

    private readonly string _dir = Directory.CreateTempSubdirectory("qlcheck_dup_").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Uncommitted_reports_only_the_changed_copy_and_names_the_committed_one()
    {
        TestGit.Init(_dir);
        Write(CopyA, Source(Daily));
        TestGit.CommitAll(_dir);
        Write(CopyB, Source(Weekly));

        var finding = Assert.Single(Run("--uncommitted"));

        Assert.Equal(CopyB, finding.File);
        Assert.Contains(CopyA, finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_suppression_comment_silences_only_its_copy()
    {
        Write(CopyA, Source(Daily, $"// qlcheck-ignore: {Id} checked-on:{TestDates.Today}"));
        Write(CopyB, Source(Weekly));

        Assert.Equal([CopyB], Run().Select(finding => finding.File));
    }

    [Fact]
    public void A_disabled_tree_hides_its_copy_but_the_copy_is_still_named()
    {
        Write(CopyA, Source(Daily));
        Write(TestCopy, Source(Weekly));
        Write("tests/.qlcheck_disable", $"{Id}\n");

        // --check ignores disable files, so this runs the default selection.
        var finding = Assert.Single(Run(selectOnly: false));

        Assert.Equal(CopyA, finding.File);
        Assert.Contains(TestCopy, finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Copies_in_two_projects_are_found()
    {
        const string Project = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n";
        Write("Api/Api.csproj", Project);
        Write("Worker/Worker.csproj", Project);
        Write("Api/A.cs", Source(Daily));
        Write("Worker/B.cs", Source(Weekly));

        Assert.Equal(["Api/A.cs", "Worker/B.cs"], Run().Select(finding => finding.File).Order(StringComparer.Ordinal));
    }

    // A method whose body, after a first statement of its own, is the shared block. Paths are
    // compared relative to the scanned directory.
    private static string Source(string name, string beforeCopy = "") => $$"""
        using System;
        using System.Collections.Generic;

        public class {{name}}Report
        {
            public void Print(List<string> lines)
            {
                Console.WriteLine("{{name}}");
                {{beforeCopy}}
                var total = 0;
                foreach (var line in lines)
                {
                    total += line.Length;
                }

                Console.WriteLine("lines " + lines.Count);
                Console.WriteLine("characters " + total);
                Console.WriteLine("average " + total / Math.Max(1, lines.Count));
            }
        }

        """;

    private void Write(string path, string text)
    {
        var full = Path.Combine(_dir, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    private List<(string File, string Message)> Run(string? flag = null, bool selectOnly = true)
    {
        string[] args = [.. flag is null ? [] : new[] { flag }, .. selectOnly ? new[] { "--check", Id } : [], _dir];
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        QlcheckApp.Run(args, stdout, stderr);
        using var json = JsonDocument.Parse(stdout.ToString());
        return [.. json.RootElement.GetProperty("findings").EnumerateArray()
            .Where(finding => finding.GetProperty("check").GetString() == Id)
            .Select(finding => (Path.GetRelativePath(_dir, finding.GetProperty("file").GetString()!), finding.GetProperty("message").GetString()!))];
    }
}

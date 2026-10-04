namespace Qlcheck.Tests;

public class QlcheckAppTests : IDisposable
{
    private const string TempPrefix = "qlcheck_";

    private const string FindingsProperty = "\"findings\"";

    private const string UsagePrefix = "Usage:";

    private const string FindingsCountPattern = @"Findings\s+\d+";

    private const string DirtyPath = "/Dirty.cs";

    private const string DurationLabel = "Duration";

    private const string HiddenInNodeModules = "node_modules/pkg/Hidden.cs";

    private const string HiddenInSkipMe = "skipme/Hidden.cs";

    private const string IgnoreFileName = ".qlcheck_ignore";

    private readonly string _dir = Directory.CreateTempSubdirectory(TempPrefix).FullName;

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Writes_json_findings_and_exits_one()
    {
        var file = Write(
            "Repo.cs",
            """
            class C
            {
                int M()
                {
                    return 42;
                }
            }
            """);

        var stdout = new StringWriter();
        var code = QlcheckApp.Run([file], stdout, new StringWriter());

        Assert.Equal(ExitCode.Findings, code);
        Assert.Contains("\"check\": \"magic-literal\"", stdout.ToString());
        Assert.Contains(FindingsProperty, stdout.ToString());
    }

    [Fact]
    public void Writes_human_output_with_flag()
    {
        var file = Write(
            "Repo.cs",
            """
            class C
            {
                int M()
                {
                    return 42;
                }
            }
            """);

        var stdout = new StringWriter();
        var code = QlcheckApp.Run(["--human", file], stdout, new StringWriter());

        Assert.Equal(ExitCode.Findings, code);
        var text = stdout.ToString();
        Assert.Contains("magic-literal", text);
        Assert.DoesNotContain(FindingsProperty, text);
    }

    [Fact]
    public void Exits_zero_when_clean()
    {
        var file = Write(
            "Repo.cs",
            """
            class C
            {
                void M()
                {
                }
            }
            """);

        var stdout = new StringWriter();
        var code = QlcheckApp.Run(["--check", "magic-literal", file], stdout, new StringWriter());

        Assert.Equal(ExitCode.Clean, code);
        Assert.Contains(FindingsProperty, stdout.ToString());
        Assert.DoesNotContain("magic-literal", stdout.ToString());
    }

    [Fact]
    public void Exits_two_when_path_missing()
    {
        var stderr = new StringWriter();
        var code = QlcheckApp.Run(["--human"], new StringWriter(), stderr);

        Assert.Equal(ExitCode.Error, code);
        Assert.Contains(UsagePrefix, stderr.ToString());
    }

    [Fact]
    public void Rejects_unknown_check()
    {
        var file = Write("Repo.cs", "class C {}");
        var stderr = new StringWriter();
        var code = QlcheckApp.Run(["--check", "nope", file], new StringWriter(), stderr);

        Assert.Equal(ExitCode.Error, code);
        Assert.Contains("Unknown check: nope", stderr.ToString());
    }

    [Fact]
    public void Default_run_skips_coverage()
    {
        var file = Write("Repo.cs", "class C { void M() { } }\n");

        var stdout = new StringWriter();
        QlcheckApp.Run([file], stdout, new StringWriter());

        Assert.DoesNotContain("\"check\": \"coverage\"", stdout.ToString());
    }

    [Fact]
    public void Coverage_flag_without_a_project_exits_two()
    {
        var file = Write("Repo.cs", "class C { void M() { } }\n");
        var stderr = new StringWriter();
        var code = QlcheckApp.Run(["--coverage", file], new StringWriter(), stderr);

        Assert.Equal(ExitCode.Error, code);
        Assert.Contains("No project for", stderr.ToString());
        Assert.DoesNotContain("Unknown option", stderr.ToString());
    }

    [Fact]
    public void Coverage_without_a_project_exits_two()
    {
        var file = Write("Repo.cs", "class C { void M() { } }\n");
        var stderr = new StringWriter();
        var code = QlcheckApp.Run(["--check", CoverageCheck.CheckId, file], new StringWriter(), stderr);

        Assert.Equal(ExitCode.Error, code);
        Assert.Contains("No project for", stderr.ToString());
    }

    [Fact]
    public void Writes_formatted_stats()
    {
        Write("Clean.cs", "class C {}\n");
        Write(
            "Dirty.cs",
            """
            class C
            {
                int M()
                {
                    return 42;
                }
            }
            """);

        var stdout = new StringWriter();
        var code = QlcheckApp.Run(["--stats", _dir], stdout, new StringWriter());

        Assert.Equal(ExitCode.Findings, code);
        var text = stdout.ToString();
        Assert.DoesNotContain(FindingsProperty, text);
        Assert.Matches(@"Files checked\s+2", text);
        Assert.Matches(@"Lines checked\s+\d+", text);
        Assert.Matches(@"Checks run\s+501", text);
        Assert.Matches(FindingsCountPattern, text);
        Assert.Matches(@"Files with findings\s+2", text);
        Assert.Matches(@"Clean files\s+0", text);
        Assert.Contains("magic-literal", text);
        Assert.Contains(DirtyPath, text);
        Assert.Contains(DurationLabel, text);
    }

    [Fact]
    public void Skips_files_no_language_matches()
    {
        Write("Notes.txt", Dirty);
        Write("Clean.cs", "class C {}\n");

        var stdout = new StringWriter();
        var code = QlcheckApp.Run(["--check", "magic-literal", _dir], stdout, new StringWriter());

        Assert.Equal(ExitCode.Clean, code);
        Assert.DoesNotContain("Notes.txt", stdout.ToString());
    }

    [Fact]
    public void Scans_directory_for_cs_files()
    {
        Write(
            "Repo.cs",
            """
            class C
            {
                int M()
                {
                    return 42;
                }
            }
            """);

        var stdout = new StringWriter();
        var code = QlcheckApp.Run([_dir], stdout, new StringWriter());

        Assert.Equal(ExitCode.Findings, code);
        Assert.Contains("magic-literal", stdout.ToString());
    }

    [Fact]
    public void Skips_node_modules_by_default()
    {
        Write("Keep.cs", Dirty);
        Write(HiddenInNodeModules, Dirty);

        var stdout = new StringWriter();
        var code = QlcheckApp.Run([_dir], stdout, new StringWriter());

        Assert.Equal(ExitCode.Findings, code);
        var text = stdout.ToString();
        Assert.Contains("Keep.cs", text);
        Assert.DoesNotContain("Hidden.cs", text);
    }

    [Fact]
    public void Skips_directories_listed_in_qlcheck_ignore()
    {
        Write("Keep.cs", Dirty);
        Write(HiddenInSkipMe, Dirty);
        Write(IgnoreFileName, """
            # generated
            skipme/
            """);

        var stdout = new StringWriter();
        var code = QlcheckApp.Run([_dir], stdout, new StringWriter());

        Assert.Equal(ExitCode.Findings, code);
        var text = stdout.ToString();
        Assert.Contains("Keep.cs", text);
        Assert.DoesNotContain("Hidden.cs", text);
    }

    [Fact]
    public void Uncommitted_reports_modified_staged_and_untracked_files()
    {
        Git("init", "-q");
        Write("Committed.cs", Dirty);
        Write("Modified.cs", "class C {}\n");
        Git("add", ".");
        Git("commit", "-q", "-m", "init");
        Write("Modified.cs", Dirty);
        Write("Staged.cs", Dirty);
        Git("add", "Staged.cs");
        Write("Untracked.cs", Dirty);

        var stdout = new StringWriter();
        var code = QlcheckApp.Run(["--uncommitted", "--check", "magic-literal", _dir], stdout, new StringWriter());

        Assert.Equal(ExitCode.Findings, code);
        var text = stdout.ToString();
        Assert.Contains("Modified.cs", text);
        Assert.Contains("Staged.cs", text);
        Assert.Contains("Untracked.cs", text);
        Assert.DoesNotContain("Committed.cs", text);
    }

    [Fact]
    public void Uncommitted_stats_count_only_changed_files()
    {
        Git("init", "-q");
        Write("Committed.cs", Dirty);
        Git("add", ".");
        Git("commit", "-q", "-m", "init");
        Write("Untracked.cs", Dirty);

        var stdout = new StringWriter();
        var code = QlcheckApp.Run(["--uncommitted", "--stats", "--check", "magic-literal", _dir], stdout, new StringWriter());

        Assert.Equal(ExitCode.Findings, code);
        Assert.Matches(@"Files checked\s+1", stdout.ToString());
    }

    [Fact]
    public void Uncommitted_is_clean_when_nothing_changed()
    {
        Git("init", "-q");
        Write("Committed.cs", Dirty);
        Git("add", ".");
        Git("commit", "-q", "-m", "init");

        var stdout = new StringWriter();
        var code = QlcheckApp.Run(["--uncommitted", _dir], stdout, new StringWriter());

        Assert.Equal(ExitCode.Clean, code);
        Assert.DoesNotContain("Committed.cs", stdout.ToString());
    }

    [Fact]
    public void Uncommitted_outside_a_git_repository_exits_two()
    {
        Write("Repo.cs", Dirty);
        var stderr = new StringWriter();

        var code = QlcheckApp.Run(["--uncommitted", _dir], new StringWriter(), stderr);

        Assert.Equal(ExitCode.Error, code);
        Assert.Contains("--uncommitted needs a git repository", stderr.ToString());
    }

    private const string Dirty =
        """
        class C
        {
            int M()
            {
                return 42;
            }
        }
        """;

    private void Git(params string[] args)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = _dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in (string[])["-c", "user.name=qlcheck", "-c", "user.email=qlcheck@example.com", "-c", "commit.gpgsign=false", .. args])
        {
            info.ArgumentList.Add(arg);
        }

        using var process = System.Diagnostics.Process.Start(info)!;
        var stderr = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, stderr);
    }

    private string Write(string name, string text)
    {
        var path = Path.Combine(_dir, name);
        var dir = Path.GetDirectoryName(path);
        if (dir is not null)
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, text);
        return path;
    }
}

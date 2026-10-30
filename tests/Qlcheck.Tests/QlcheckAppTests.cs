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

    private const string DisableFileName = ".qlcheck_disable";

    private const string MagicLiteral = "magic-literal";

    private const string HumanFlag = "--human";

    private const string InitCommand = "init";

    private const string QuietFlag = "-q";

    private const string AddCommand = "add";

    private const string GitIgnore = ".gitignore";

    private const string EnvFile = ".env";

    private const string EnvBody = "A=1\n";

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

                int N() => 42;
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

                int N() => 42;
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

                int N() => 42;
            }
            """);

        var stdout = new StringWriter();
        var code = QlcheckApp.Run(["--stats", _dir], stdout, new StringWriter());

        Assert.Equal(ExitCode.Findings, code);
        var text = stdout.ToString();
        Assert.DoesNotContain(FindingsProperty, text);
        Assert.Matches(@"Files checked\s+2", text);
        Assert.Matches(@"Lines checked\s+\d+", text);
        Assert.Matches(@"Checks run\s+329", text);
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

                int N() => 42;
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
        Git(InitCommand, QuietFlag);
        Write("Committed.cs", Dirty);
        Write("Modified.cs", "class C {}\n");
        Git(AddCommand, ".");
        Git("commit", QuietFlag, "-m", InitCommand);
        Write("Modified.cs", Dirty);
        Write("Staged.cs", Dirty);
        Git(AddCommand, "Staged.cs");
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
        Git(InitCommand, QuietFlag);
        Write("Committed.cs", Dirty);
        Git(AddCommand, ".");
        Git("commit", QuietFlag, "-m", InitCommand);
        Write("Untracked.cs", Dirty);

        var stdout = new StringWriter();
        var code = QlcheckApp.Run(["--uncommitted", "--stats", "--check", "magic-literal", _dir], stdout, new StringWriter());

        Assert.Equal(ExitCode.Findings, code);
        Assert.Matches(@"Files checked\s+1", stdout.ToString());
    }

    [Fact]
    public void Uncommitted_is_clean_when_nothing_changed()
    {
        Git(InitCommand, QuietFlag);
        Write("Committed.cs", Dirty);
        Git(AddCommand, ".");
        Git("commit", QuietFlag, "-m", InitCommand);

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

    [Fact]
    public void Disable_file_turns_a_check_off()
    {
        Write("Dirty.cs", Dirty);
        Write(DisableFileName, "magic-literal  # tests repeat literals\n");

        var stdout = new StringWriter();
        QlcheckApp.Run([HumanFlag, _dir], stdout, new StringWriter());

        Assert.DoesNotContain(MagicLiteral, stdout.ToString());
    }

    [Fact]
    public void Check_flag_overrides_the_disable_file()
    {
        Write("Dirty.cs", Dirty);
        Write(DisableFileName, "magic-literal\n");

        var stdout = new StringWriter();
        var code = QlcheckApp.Run(["--check", MagicLiteral, HumanFlag, _dir], stdout, new StringWriter());

        Assert.Equal(ExitCode.Findings, code);
        Assert.Contains(MagicLiteral, stdout.ToString());
    }

    [Fact]
    public void Disable_file_in_a_subfolder_only_covers_that_folder()
    {
        Write("src/Dirty.cs", Dirty);
        Write("tests/Dirty.cs", Dirty);
        Write("tests/" + DisableFileName, "magic-literal\n");

        var stdout = new StringWriter();
        QlcheckApp.Run([HumanFlag, _dir], stdout, new StringWriter());

        var text = stdout.ToString();
        Assert.Matches(@"src/Dirty\.cs:\d+:\d+  magic-literal", text);
        Assert.DoesNotMatch(@"tests/Dirty\.cs:\d+:\d+  magic-literal", text);
    }

    [Fact]
    public void Unknown_id_in_the_disable_file_exits_two()
    {
        Write("Dirty.cs", Dirty);
        Write(DisableFileName, "magic-literals\n");
        var stderr = new StringWriter();

        var code = QlcheckApp.Run([_dir], new StringWriter(), stderr);

        Assert.Equal(ExitCode.Error, code);
        Assert.Contains("Unknown check: magic-literals", stderr.ToString());
    }

    [Fact]
    public void Json_and_stats_list_disabled_checks_with_their_folder()
    {
        Write("tests/Dirty.cs", Dirty);
        Write("tests/" + DisableFileName, "magic-literal\n");

        var json = new StringWriter();
        QlcheckApp.Run([_dir], json, new StringWriter());
        var stats = new StringWriter();
        QlcheckApp.Run(["--stats", _dir], stats, new StringWriter());

        Assert.Contains("\"disabled\"", json.ToString());
        Assert.Contains("\"under\": \"tests/\"", json.ToString());
        Assert.Matches(@"Disabled\s+│", stats.ToString());
        Assert.Matches(@"magic-literal\s+tests/", stats.ToString());
    }

    private const string Dirty =
        """
        class C
        {
            int M()
            {
                return 42;
            }

            int N() => 42;
        }
        """;

    [Fact]
    public void Tracked_lists_only_files_git_tracks()
    {
        Git(InitCommand, QuietFlag);
        File.WriteAllText(Path.Combine(_dir, GitIgnore), "ignored.env\n");
        var tracked = Write(EnvFile, EnvBody);
        var untracked = Write(".env.local", EnvBody);
        var ignored = Write("ignored.env", EnvBody);
        Git(AddCommand, EnvFile, GitIgnore);

        var files = GitChanges.Tracked(_dir);

        Assert.Contains(Path.GetFullPath(tracked), files);
        Assert.DoesNotContain(Path.GetFullPath(untracked), files);
        Assert.DoesNotContain(Path.GetFullPath(ignored), files);
    }

    [Fact]
    public void Tracked_is_empty_outside_a_repository_and_does_not_throw()
    {
        Write(EnvFile, EnvBody);

        Assert.Empty(GitChanges.Tracked(_dir));
    }

    private void Git(params string[] args) => TestGit.Run(_dir, args);

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

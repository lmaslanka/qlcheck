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
        Assert.Contains(MagicLiteralCheck.CheckId, text);
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
        var code = QlcheckApp.Run(["--check", MagicLiteralCheck.CheckId, file], stdout, new StringWriter());

        Assert.Equal(ExitCode.Clean, code);
        Assert.Contains(FindingsProperty, stdout.ToString());
        Assert.DoesNotContain(MagicLiteralCheck.CheckId, stdout.ToString());
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
    public void Inline_sql_flag_reports_layout_finding()
    {
        var file = Write("Repo.cs", InlineSql);

        var stdout = new StringWriter();
        var code = QlcheckApp.Run(["--inline-sql", file], stdout, new StringWriter());

        Assert.Equal(ExitCode.Findings, code);
        Assert.Contains("\"check\": \"inline-sql\"", stdout.ToString());
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
    public void Default_run_skips_inline_sql()
    {
        var file = Write("Repo.cs", InlineSql);

        var stdout = new StringWriter();
        var code = QlcheckApp.Run([file], stdout, new StringWriter());

        Assert.NotEqual(ExitCode.Error, code);
        Assert.DoesNotContain(InlineSqlCheck.CheckId, stdout.ToString());
    }

    [Fact]
    public void Check_flag_enables_inline_sql_exclusively()
    {
        var file = Write("Repo.cs", InlineSql);

        var stdout = new StringWriter();
        var code = QlcheckApp.Run(["--check", "inline-sql", file], stdout, new StringWriter());

        Assert.Equal(ExitCode.Findings, code);
        var text = stdout.ToString();
        Assert.Contains("\"check\": \"inline-sql\"", text);
        Assert.DoesNotContain(MagicLiteralCheck.CheckId, text);
    }

    [Fact]
    public void Check_filter_excludes_inline_sql_even_with_flag()
    {
        var file = Write("Repo.cs", InlineSql);

        var stdout = new StringWriter();
        var code = QlcheckApp.Run(["--check", "magic-literal", "--inline-sql", file], stdout, new StringWriter());

        Assert.Equal(ExitCode.Clean, code);
        Assert.DoesNotContain(InlineSqlCheck.CheckId, stdout.ToString());
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
        Assert.Matches(@"Checks run\s+502", text);
        Assert.Matches(FindingsCountPattern, text);
        Assert.Matches(@"Files with findings\s+2", text);
        Assert.Matches(@"Clean files\s+0", text);
        Assert.Contains(MagicLiteralCheck.CheckId, text);
        Assert.Contains(DirtyPath, text);
        Assert.Contains(DurationLabel, text);
    }

    [Fact]
    public void Skips_files_no_language_matches()
    {
        Write("Notes.txt", Dirty);
        Write("Clean.cs", "class C {}\n");

        var stdout = new StringWriter();
        var code = QlcheckApp.Run(["--check", MagicLiteralCheck.CheckId, _dir], stdout, new StringWriter());

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
        Assert.Contains(MagicLiteralCheck.CheckId, stdout.ToString());
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

    private const string InlineSql =
        """
        class C
        {
            void M()
            {
                connection.QueryAsync("select 1");
            }
        }
        """;

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

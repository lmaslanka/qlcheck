namespace Qlcheck.Tests;

public class QlcheckAppTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("qlcheck_").FullName;

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
        Assert.Contains("\"findings\"", stdout.ToString());
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
        Assert.DoesNotContain("\"findings\"", text);
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
        var code = QlcheckApp.Run([file], stdout, new StringWriter());

        Assert.Equal(ExitCode.Clean, code);
        Assert.Contains("\"findings\"", stdout.ToString());
        Assert.DoesNotContain("magic-literal", stdout.ToString());
    }

    [Fact]
    public void Exits_two_when_path_missing()
    {
        var stderr = new StringWriter();
        var code = QlcheckApp.Run(["--human"], new StringWriter(), stderr);

        Assert.Equal(ExitCode.Error, code);
        Assert.Contains("Usage:", stderr.ToString());
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
    public void Default_run_skips_inline_sql()
    {
        var file = Write("Repo.cs", InlineSql);

        var stdout = new StringWriter();
        var code = QlcheckApp.Run([file], stdout, new StringWriter());

        Assert.Equal(ExitCode.Clean, code);
        Assert.DoesNotContain("inline-sql", stdout.ToString());
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
        Assert.DoesNotContain("magic-literal", text);
    }

    [Fact]
    public void Check_filter_excludes_inline_sql_even_with_flag()
    {
        var file = Write("Repo.cs", InlineSql);

        var stdout = new StringWriter();
        var code = QlcheckApp.Run(["--check", "magic-literal", "--inline-sql", file], stdout, new StringWriter());

        Assert.Equal(ExitCode.Clean, code);
        Assert.DoesNotContain("inline-sql", stdout.ToString());
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
        Assert.DoesNotContain("\"findings\"", text);
        Assert.Matches(@"Files checked\s+2", text);
        Assert.Matches(@"Lines checked\s+\d+", text);
        Assert.Matches(@"Checks run\s+6", text);
        Assert.Matches(@"Findings\s+1", text);
        Assert.Matches(@"Files with findings\s+1", text);
        Assert.Matches(@"Clean files\s+1", text);
        Assert.Contains("magic-literal", text);
        Assert.Contains("/Dirty.cs", text);
        Assert.Contains("Duration", text);
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
        Write("node_modules/pkg/Hidden.cs", Dirty);

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
        Write("skipme/Hidden.cs", Dirty);
        Write(".qlcheck_ignore", """
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

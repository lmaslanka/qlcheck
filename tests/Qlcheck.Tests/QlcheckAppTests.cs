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
                void M()
                {
                    connection.QueryAsync("select 1");
                }
            }
            """);

        var stdout = new StringWriter();
        var code = QlcheckApp.Run([file], stdout, new StringWriter());

        Assert.Equal(ExitCode.Findings, code);
        Assert.Contains("\"check\": \"inline-sql\"", stdout.ToString());
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
                void M()
                {
                    connection.QueryAsync("select 1");
                }
            }
            """);

        var stdout = new StringWriter();
        var code = QlcheckApp.Run(["--human", file], stdout, new StringWriter());

        Assert.Equal(ExitCode.Findings, code);
        var text = stdout.ToString();
        Assert.Contains("inline-sql", text);
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
        Assert.DoesNotContain("inline-sql", stdout.ToString());
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
    public void Writes_formatted_stats()
    {
        Write("Clean.cs", "class C {}\n");
        Write(
            "Dirty.cs",
            """
            class C
            {
                void M()
                {
                    connection.QueryAsync("select 1");
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
        Assert.Matches(@"Checks run\s+7", text);
        Assert.Matches(@"Findings\s+1", text);
        Assert.Matches(@"Files with findings\s+1", text);
        Assert.Matches(@"Clean files\s+1", text);
        Assert.Contains("inline-sql", text);
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
                void M()
                {
                    connection.Execute("select 1");
                }
            }
            """);

        var stdout = new StringWriter();
        var code = QlcheckApp.Run([_dir], stdout, new StringWriter());

        Assert.Equal(ExitCode.Findings, code);
        Assert.Contains("inline-sql", stdout.ToString());
    }

    [Fact]
    public void Skips_directories_listed_in_qlcheck_ignore()
    {
        Write("Keep.cs", Query);
        Write("skipme/Hidden.cs", Query);
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

    private const string Query =
        """
        class C
        {
            void M()
            {
                connection.QueryAsync("select 1");
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

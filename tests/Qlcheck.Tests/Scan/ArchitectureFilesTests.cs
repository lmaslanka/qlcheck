namespace Qlcheck.Tests;

public sealed class ArchitectureFilesTests : IDisposable
{
    private const string FileName = ".qlcheck_architecture";

    private const string GitDirectory = ".git";

    private const string Source = "src";

    private const string Separator = ",";

    private readonly string _repo = Directory.CreateTempSubdirectory("qlcheck_arch_").FullName;

    public void Dispose() => Directory.Delete(_repo, recursive: true);

    [Fact]
    public void Parses_the_three_kinds_skipping_comments_and_blanks()
    {
        Write(FileName, """
            # The intended architecture.

            Qlcheck.Cli Qlcheck.Report  must-not-use  Qlcheck.Run   # layering
            System.Diagnostics.Process  only-used-by  Qlcheck.Scan.GitChanges
            no-cycles Qlcheck.*
            """);

        var file = Assert.Single(Load().Files);

        Assert.Equal(
            [
                "MustNotUse Qlcheck.Cli,Qlcheck.Report -> Qlcheck.Run :3",
                "OnlyUsedBy Qlcheck.Scan.GitChanges -> System.Diagnostics.Process :4",
                "NoCycles Qlcheck ->  :5",
            ],
            file.Constraints.Select(constraint => $"{constraint.Kind} {string.Join(Separator, constraint.Sources)} -> {string.Join(Separator, constraint.Targets)} :{constraint.Line}"));
    }

    [Theory]
    [InlineData("Qlcheck.Cli depends-on Qlcheck.Run", "Unknown constraint")]
    [InlineData("must-not-use Qlcheck.Run", "needs names on both sides")]
    [InlineData("Qlcheck.Cli must-not-use", "needs names on both sides")]
    [InlineData("Qlcheck..Cli must-not-use Qlcheck.Run", "Not a dotted name: 'Qlcheck..Cli'")]
    [InlineData("Qlcheck.* must-not-use Qlcheck.Run", "Not a dotted name: 'Qlcheck.*'")]
    [InlineData("no-cycles Qlcheck", "no-cycles needs one name ending in '.*'")]
    [InlineData("no-cycles Qlcheck.* Other.*", "no-cycles needs one name ending in '.*'")]
    public void A_malformed_line_is_an_error_naming_the_file_and_line(string line, string error)
    {
        Write(FileName, $"# header\n{line}\n");

        var ex = Assert.Throws<InvalidOperationException>(Load);

        Assert.Contains(error, ex.Message, StringComparison.Ordinal);
        Assert.EndsWith($"({Path.Combine(_repo, FileName)}:2)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_git_root_file_applies_to_a_subfolder_scan()
    {
        Directory.CreateDirectory(Path.Combine(_repo, GitDirectory));
        Write(FileName, "A must-not-use B\n");
        var source = Write("src/App/A.cs", "class A { }\n");

        var files = ArchitectureFiles.Load([Path.Combine(_repo, Source)], [source]);

        Assert.Equal(Path.Combine(_repo, FileName), files.For(source)?.Path);
    }

    [Fact]
    public void Outside_git_the_scan_root_is_the_place()
    {
        Write($"{Source}/{FileName}", "A must-not-use B\n");
        var source = Write($"{Source}/A.cs", "class A { }\n");

        Assert.NotNull(ArchitectureFiles.Load([Path.Combine(_repo, Source)], [source]).For(source));
    }

    [Fact]
    public void A_file_below_the_root_is_an_error()
    {
        Directory.CreateDirectory(Path.Combine(_repo, GitDirectory));
        Write($"{Source}/{FileName}", "A must-not-use B\n");
        var source = Write($"{Source}/A.cs", "class A { }\n");

        var ex = Assert.Throws<InvalidOperationException>(() => ArchitectureFiles.Load([_repo], [source]));

        Assert.Contains("must be at the repository root", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_repositories_have_their_own_files()
    {
        var first = Path.Combine(_repo, "one");
        var second = Path.Combine(_repo, "two");
        Directory.CreateDirectory(Path.Combine(first, GitDirectory));
        Directory.CreateDirectory(Path.Combine(second, GitDirectory));
        Write($"one/{FileName}", "A must-not-use B\n");
        var a = Write("one/A.cs", "class A { }\n");
        var b = Write("two/B.cs", "class B { }\n");

        var files = ArchitectureFiles.Load([first, second], [a, b]);

        Assert.NotNull(files.For(a));
        Assert.Null(files.For(b));
    }

    [Fact]
    public void No_file_means_nothing_applies()
    {
        var source = Write("A.cs", "class A { }\n");

        Assert.Empty(ArchitectureFiles.Load([_repo], [source]).Files);
    }

    [Theory]
    [InlineData("Qlcheck.Scan", "Qlcheck.Scan", true)]
    [InlineData("Qlcheck.Scan", "Qlcheck.Scan.GitChanges.Run", true)]
    [InlineData("Qlcheck.Scan", "Qlcheck.Scanner", false)]
    [InlineData("Qlcheck.Scan", "Qlcheck", false)]
    public void A_dotted_name_matches_itself_and_what_is_below_it(string pattern, string name, bool matches)
    {
        Assert.Equal(matches, DottedNames.Matches(pattern, name));
    }

    private ArchitectureFiles Load() => ArchitectureFiles.Load([_repo], [Write("A.cs", "class A { }\n")]);

    private string Write(string path, string text)
    {
        var full = Path.Combine(_repo, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
        return full;
    }
}

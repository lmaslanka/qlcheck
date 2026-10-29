namespace Qlcheck.Tests;

public class DisableFilesTests : IDisposable
{
    private const string DisableFile = ".qlcheck_disable";

    private const string MagicLiteral = "magic-literal";

    private const string FileLength = "file-length";

    private readonly string _repo = Directory.CreateTempSubdirectory("qlcheck_disable_").FullName;

    public DisableFilesTests()
    {
        Directory.CreateDirectory(Path.Combine(_repo, ".git"));
    }

    public void Dispose()
    {
        Directory.Delete(_repo, recursive: true);
    }

    [Fact]
    public void Root_file_disables_a_check_for_every_file()
    {
        Write(DisableFile, "magic-literal\n");
        var app = Path.Combine(_repo, "src/App.cs");

        var files = DisableFiles.Load([_repo], [app]);

        Assert.True(files.IsDisabled(MagicLiteral, app));
        Assert.False(files.IsDisabled(FileLength, app));
    }

    [Fact]
    public void Subfolder_file_disables_a_check_only_below_its_folder()
    {
        Write("tests/" + DisableFile, "magic-literal\n");
        var test = Path.Combine(_repo, "tests/AppTests.cs");
        var app = Path.Combine(_repo, "src/App.cs");

        var files = DisableFiles.Load([_repo], [test, app]);

        Assert.True(files.IsDisabled(MagicLiteral, test));
        Assert.False(files.IsDisabled(MagicLiteral, app));
        var entry = Assert.Single(files.Disabled);
        Assert.Equal("tests/", entry.Under);
    }

    [Fact]
    public void Bang_turns_a_check_back_on_below_its_folder()
    {
        Write(DisableFile, "file-length\n");
        Write("src/core/" + DisableFile, "!file-length\n");
        var core = Path.Combine(_repo, "src/core/Engine.cs");
        var app = Path.Combine(_repo, "src/App.cs");

        var files = DisableFiles.Load([_repo], [core, app]);

        Assert.False(files.IsDisabled(FileLength, core));
        Assert.True(files.IsDisabled(FileLength, app));
        Assert.Equal([FileLength], files.Disabled.Select(entry => entry.Check));
    }

    [Fact]
    public void Root_file_applies_when_scanning_a_subfolder()
    {
        Write(DisableFile, "magic-literal\n");
        var app = Path.Combine(_repo, "src/App.cs");

        var files = DisableFiles.Load([Path.Combine(_repo, "src")], [app]);

        Assert.True(files.IsDisabled(MagicLiteral, app));
        Assert.Equal(".", Assert.Single(files.Disabled).Under);
    }

    [Fact]
    public void Skips_comments_and_blank_lines_and_keeps_line_numbers()
    {
        Write(DisableFile, "# tests repeat literals\n\nmagic-literal   # on purpose\n  \n");
        var app = Path.Combine(_repo, "App.cs");

        var entry = Assert.Single(DisableFiles.Load([_repo], [app]).Entries);

        Assert.Equal(MagicLiteral, entry.Check);
        Assert.Equal(3, entry.Line);
    }

    [Fact]
    public void Without_drops_checks_asked_for_by_name()
    {
        Write(DisableFile, "magic-literal\nfile-length\n");
        var app = Path.Combine(_repo, "App.cs");

        var files = DisableFiles.Load([_repo], [app]).Without([MagicLiteral]);

        Assert.False(files.IsDisabled(MagicLiteral, app));
        Assert.True(files.IsDisabled(FileLength, app));
        Assert.Equal([FileLength], files.Disabled.Select(entry => entry.Check));
    }

    [Fact]
    public void Files_above_the_scan_root_are_not_used_outside_a_git_repository()
    {
        Directory.Delete(Path.Combine(_repo, ".git"));
        Write(DisableFile, "magic-literal\n");
        Write("src/App.cs", string.Empty);
        var app = Path.Combine(_repo, "src/App.cs");

        var files = DisableFiles.Load([Path.Combine(_repo, "src")], [app]);

        Assert.False(files.IsDisabled(MagicLiteral, app));
        Assert.Empty(files.Entries);
    }

    private void Write(string relative, string text)
    {
        var path = Path.Combine(_repo, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}

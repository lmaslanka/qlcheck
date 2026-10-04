using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Qlcheck.Scan;

internal static class GitChanges
{
    private const string GitExecutable = "git";

    private const string ChangeDirectoryFlag = "-C";

    private const char NulSeparator = '\0';

    private static readonly string[] UnstagedArgs = ["diff", "--name-only", "--relative", "--diff-filter=d", "-z"];

    private static readonly string[] UntrackedArgs = ["ls-files", "--others", "--exclude-standard", "-z"];

    // Full paths of files under the given paths with unstaged changes: tracked files modified in the
    // working tree but not staged, plus untracked files git does not ignore. Deleted files are left out.
    public static IReadOnlySet<string> Unstaged(IReadOnlyList<string> paths)
    {
        var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in paths.Select(DirectoryOf).Distinct(StringComparer.Ordinal))
        {
            AddAll(changed, dir, UnstagedArgs);
            AddAll(changed, dir, UntrackedArgs);
        }

        return changed;
    }

    private static string DirectoryOf(string path)
    {
        var full = Path.GetFullPath(path);
        if (Directory.Exists(full))
        {
            return full;
        }

        return Path.GetDirectoryName(full)
               ?? throw new DirectoryNotFoundException($"Path not found: {path}");
    }

    private static void AddAll(HashSet<string> changed, string dir, IReadOnlyList<string> args) =>
        changed.UnionWith(
            Run(dir, args)
                .Split(NulSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(relative => Path.GetFullPath(Path.Combine(dir, relative))));

    private static string Run(string dir, IReadOnlyList<string> args)
    {
        var info = new ProcessStartInfo(GitExecutable, [ChangeDirectoryFlag, dir, .. args])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        var stderr = new StringBuilder();
        using var process = new Process { StartInfo = info };
        process.ErrorDataReceived += (_, e) => stderr.AppendLine(e.Data);
        try
        {
            process.Start();
        }
        catch (Win32Exception)
        {
            throw new InvalidOperationException("git was not found on PATH.");
        }

        process.BeginErrorReadLine();
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"--unstaged needs a git repository: {stderr.ToString().Trim()}");
        }

        return stdout;
    }
}

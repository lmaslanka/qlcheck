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

    private static readonly string[] StagedArgs = ["diff", "--cached", "--name-only", "--relative", "--diff-filter=d", "-z"];

    private static readonly string[] UntrackedArgs = ["ls-files", "--others", "--exclude-standard", "-z"];

    private static readonly string[] TrackedArgs = ["ls-files", "-z"];

    // Full paths of files under the given paths that are not committed: tracked files with staged
    // or unstaged changes, plus untracked files git does not ignore. Deleted files are left out.
    public static IReadOnlySet<string> Uncommitted(IReadOnlyList<string> paths)
    {
        var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in paths.Select(DirectoryOf).Distinct(StringComparer.Ordinal))
        {
            AddAll(changed, dir, UnstagedArgs);
            AddAll(changed, dir, StagedArgs);
            AddAll(changed, dir, UntrackedArgs);
        }

        return changed;
    }

    // Full paths of every file git tracks under the given folder. Empty, never an error, when git
    // is missing or the folder is not in a repository: callers use it to decide what to scan.
    public static IReadOnlySet<string> Tracked(string gitRoot)
    {
        var root = Path.GetFullPath(gitRoot);
        var output = TryRun(root, TrackedArgs);
        return output is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : Paths(root, output).ToHashSet(StringComparer.OrdinalIgnoreCase);
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
        changed.UnionWith(Paths(dir, Run(dir, args)));

    private static IEnumerable<string> Paths(string dir, string output) =>
        output
            .Split(NulSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(relative => Path.GetFullPath(Path.Combine(dir, relative)));

    private static string Run(string dir, IReadOnlyList<string> args)
    {
        GitResult result;
        try
        {
            result = Start(dir, args);
        }
        catch (Win32Exception)
        {
            throw new InvalidOperationException("git was not found on PATH.");
        }

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"--uncommitted needs a git repository: {result.Stderr.Trim()}");
        }

        return result.Stdout;
    }

    // The output, or null when git is missing or fails.
    private static string? TryRun(string dir, IReadOnlyList<string> args)
    {
        try
        {
            var result = Start(dir, args);
            return result.ExitCode == 0 ? result.Stdout : null;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    private static GitResult Start(string dir, IReadOnlyList<string> args)
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
        process.Start();
        process.BeginErrorReadLine();
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return new GitResult(process.ExitCode, stdout, stderr.ToString());
    }

    private readonly record struct GitResult(int ExitCode, string Stdout, string Stderr);
}

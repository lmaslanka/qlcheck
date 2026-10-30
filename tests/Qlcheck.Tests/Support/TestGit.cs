using System.Diagnostics;

namespace Qlcheck.Tests;

// Runs git in a temporary test repository with a fixed identity and no signing.
internal static class TestGit
{
    private static readonly string[] Identity =
        ["-c", "user.name=qlcheck", "-c", "user.email=qlcheck@example.com", "-c", "commit.gpgsign=false"];

    public static void Run(string directory, params string[] args)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in Identity.Concat(args))
        {
            info.ArgumentList.Add(arg);
        }

        using var process = Process.Start(info)!;
        var stderr = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, stderr);
    }
}

namespace Qlcheck.Tests;

// The repository root, found by walking up from the test binaries to the solution file, so tests
// read committed files from the source tree rather than the build output.
internal static class RepoPaths
{
    private const string SolutionName = "Qlcheck.slnx";

    public static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, SolutionName)))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repo root was not found.");
    }
}

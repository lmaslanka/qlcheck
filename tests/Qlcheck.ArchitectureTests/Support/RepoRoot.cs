namespace Qlcheck.ArchitectureTests.Support;

using System;
using System.IO;

/// <summary>Locates the repository root on disk, independent of the test output folder.</summary>
public static class RepoRoot
{
    private const string MarkerFile = "Qlcheck.slnx";

    public static string Path { get; } = Find(AppContext.BaseDirectory);

    private static string Find(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, MarkerFile)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException($"Could not locate {MarkerFile} above {start}.");
    }

    public static string RelativePath(string absolutePath) =>
        System.IO.Path.GetRelativePath(Path, absolutePath).Replace('\\', '/');
}

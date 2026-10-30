// Copyright (c) qlcheck contributors.
namespace Qlcheck.Scan;

// Whether a path belongs to tests. Substring-based on purpose, so `Latest.cs` counts too; every
// Language shares this one rule.
internal static class TestPaths
{
    private const string TestMarker = "Test";

    public static bool IsTest(string path) => path.Contains(TestMarker, StringComparison.OrdinalIgnoreCase);
}

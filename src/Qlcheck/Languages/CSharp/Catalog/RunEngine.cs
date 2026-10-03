// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog;

internal static class RunEngine
{
    private const string CoverageId = "coverage";

    public static IReadOnlyDictionary<string, CheckClass> Rows { get; } =
        new Dictionary<string, CheckClass>(StringComparer.Ordinal)
        {
            [CoverageId] = CheckClass.Process,
        };
}

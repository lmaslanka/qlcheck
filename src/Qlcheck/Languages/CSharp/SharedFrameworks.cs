// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;

namespace Qlcheck.Languages.CSharp;

// Shared frameworks a project gets from a FrameworkReference rather than a package, so they never
// appear in project.assets.json. Today that is ASP.NET Core, taken from the reference pack installed
// next to the running runtime (ADR 0011).
internal static class SharedFrameworks
{
    private const string AspNetCoreApp = "Microsoft.AspNetCore.App";

    private const string RefPackSuffix = ".Ref";

    private const string PacksDir = "packs";

    private const string SharedDir = "shared";

    private const string RefDir = "ref";

    private const string DllPattern = "*.dll";

    private const int RuntimeDirDepth = 3;

    private static readonly string[] WebSdks =
    [
        "Microsoft.NET.Sdk.Web",
        "Microsoft.NET.Sdk.Razor",
        "Microsoft.NET.Sdk.BlazorWebAssembly",
    ];

    private static readonly Lazy<MetadataReference[]> AspNetCoreReferences = new(LoadAspNetCore);

    public static IReadOnlyList<MetadataReference> For(ProjectSettings settings) =>
        UsesAspNetCore(settings) ? AspNetCoreReferences.Value : [];

    public static IReadOnlyList<MetadataReference> AspNetCore() => AspNetCoreReferences.Value;

    private static bool UsesAspNetCore(ProjectSettings settings) =>
        WebSdks.Contains(settings.Sdk, StringComparer.OrdinalIgnoreCase)
        || settings.FrameworkReferences.Contains(AspNetCoreApp, StringComparer.OrdinalIgnoreCase);

    // The reference pack is preferred; the runtime copy is the fallback. With neither, web types stay
    // unresolved and the trees that use them are skipped, which is the safe direction.
    private static MetadataReference[] LoadAspNetCore()
    {
        var root = DotnetRoot();
        if (root is null)
        {
            return [];
        }

        var major = Environment.Version.Major;
        var pack = HighestVersion(Path.Combine(root, PacksDir, AspNetCoreApp + RefPackSuffix), major);
        var dir = pack is null ? null : Path.Combine(pack, RefDir, $"net{major}.0");
        if (dir is null || !Directory.Exists(dir))
        {
            dir = HighestVersion(Path.Combine(root, SharedDir, AspNetCoreApp), major);
        }

        if (dir is null)
        {
            return [];
        }

        return Directory.GetFiles(dir, DllPattern)
            .Order(StringComparer.Ordinal)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();
    }

    // The core library lives in <dotnet root>/shared/Microsoft.NETCore.App/<version>/.
    private static string? DotnetRoot()
    {
        var dir = Path.GetDirectoryName(typeof(object).Assembly.Location);
        for (var level = 0; level < RuntimeDirDepth && !string.IsNullOrEmpty(dir); level++)
        {
            dir = Path.GetDirectoryName(dir);
        }

        return string.IsNullOrEmpty(dir) ? null : dir;
    }

    private static string? HighestVersion(string dir, int major)
    {
        if (!Directory.Exists(dir))
        {
            return null;
        }

        return Directory.GetDirectories(dir)
            .Select(path => (Path: path, Version: Version.TryParse(Path.GetFileName(path), out var v) ? v : null))
            .Where(candidate => candidate.Version?.Major == major)
            .OrderByDescending(candidate => candidate.Version)
            .Select(candidate => candidate.Path)
            .FirstOrDefault();
    }
}

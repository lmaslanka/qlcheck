// Copyright (c) qlcheck contributors.
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace Qlcheck.Languages.CSharp;

internal static class ProjectReferences
{
    private const string HintPathName = "HintPath";

    private const string AssetsFile = "project.assets.json";

    private const string ObjDir = "obj";

    private const string CompileName = "compile";

    private const string PackageFolders = "packageFolders";

    private const string Libraries = "libraries";

    private const string Targets = "targets";

    private const string PathName = "path";

    private const string TypeName = "type";

    private const string PackageType = "package";

    private const string DllSuffix = ".dll";

    public static IReadOnlyList<MetadataReference> Load(string csproj)
    {
        var refs = new List<MetadataReference>();
        AddHintPaths(csproj, refs);
        AddAssets(csproj, refs);
        return refs;
    }

    private static void AddHintPaths(string csproj, List<MetadataReference> refs)
    {
        var doc = XDocument.Load(csproj);
        var dir = Path.GetDirectoryName(csproj) ?? string.Empty;
        foreach (var hint in doc.Descendants().Where(element => element.Name.LocalName == HintPathName))
        {
            var path = Path.GetFullPath(Path.Combine(dir, hint.Value.Trim()));
            AddFile(refs, path);
        }
    }

    private static void AddAssets(string csproj, List<MetadataReference> refs)
    {
        var assets = Path.Combine(Path.GetDirectoryName(csproj) ?? string.Empty, ObjDir, AssetsFile);
        if (!File.Exists(assets))
        {
            return;
        }

        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(assets));
        var root = doc.RootElement;
        if (!root.TryGetProperty(PackageFolders, out var folders) || !root.TryGetProperty(Libraries, out var libraries))
        {
            return;
        }

        var folder = FirstName(folders);
        if (folder.Length == 0 || !root.TryGetProperty(Targets, out var targets))
        {
            return;
        }

        AddTargetLibs(refs, targets, libraries, folder);
    }

    private static void AddTargetLibs(
        List<MetadataReference> refs,
        System.Text.Json.JsonElement targets,
        System.Text.Json.JsonElement libraries,
        string folder)
    {
        foreach (var target in targets.EnumerateObject())
        {
            AddLibs(refs, target.Value, libraries, folder);
        }
    }

    private static void AddLibs(
        List<MetadataReference> refs,
        System.Text.Json.JsonElement libs,
        System.Text.Json.JsonElement libraries,
        string folder)
    {
        foreach (var lib in libs.EnumerateObject())
        {
            if (!libraries.TryGetProperty(lib.Name, out var library))
            {
                continue;
            }

            if (!IsPackage(library) || !lib.Value.TryGetProperty(CompileName, out var compile))
            {
                continue;
            }

            AddCompile(refs, compile, folder, LibraryPath(library));
        }
    }

    private static void AddCompile(
        List<MetadataReference> refs,
        System.Text.Json.JsonElement compile,
        string folder,
        string libraryPath)
    {
        foreach (var file in compile.EnumerateObject())
        {
            if (!file.Name.EndsWith(DllSuffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AddFile(refs, Path.Combine(folder, libraryPath, file.Name));
        }
    }

    private static bool IsPackage(System.Text.Json.JsonElement library) =>
        library.TryGetProperty(TypeName, out var type) && type.GetString() == PackageType;

    private static string LibraryPath(System.Text.Json.JsonElement library) =>
        library.TryGetProperty(PathName, out var path) ? path.GetString() ?? string.Empty : string.Empty;

    private static string FirstName(System.Text.Json.JsonElement element)
    {
        foreach (var item in element.EnumerateObject())
        {
            return item.Name;
        }

        return string.Empty;
    }

    private static void AddFile(List<MetadataReference> refs, string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        refs.Add(MetadataReference.CreateFromFile(path));
    }
}

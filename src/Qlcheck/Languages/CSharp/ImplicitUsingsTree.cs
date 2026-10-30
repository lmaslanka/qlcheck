// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Qlcheck.Languages.CSharp;

// The project's global usings as a context tree (ADR 0011): the build's own generated file when it
// is up to date, otherwise the same list synthesized from ImplicitUsings, the SDK and <Using> items.
internal static class ImplicitUsingsTree
{
    private const string ObjDir = "obj";

    private const string GeneratedSuffix = ".GlobalUsings.g.cs";

    private const string SynthesizedSuffix = ".GlobalUsings.qlcheck.g.cs";

    private const string WebSdk = "Microsoft.NET.Sdk.Web";

    private const string WorkerSdk = "Microsoft.NET.Sdk.Worker";

    private const string Sdk = "Microsoft.NET.Sdk";

    private static readonly string[] SdkUsings =
    [
        "System",
        "System.Collections.Generic",
        "System.IO",
        "System.Linq",
        "System.Net.Http",
        "System.Threading",
        "System.Threading.Tasks",
    ];

    private static readonly string[] ExtensionsUsings =
    [
        "Microsoft.Extensions.Configuration",
        "Microsoft.Extensions.DependencyInjection",
        "Microsoft.Extensions.Hosting",
        "Microsoft.Extensions.Logging",
    ];

    private static readonly string[] WebUsings =
    [
        "System.Net.Http.Json",
        "Microsoft.AspNetCore.Builder",
        "Microsoft.AspNetCore.Hosting",
        "Microsoft.AspNetCore.Http",
        "Microsoft.AspNetCore.Routing",
        .. ExtensionsUsings,
    ];

    public static SyntaxTree? For(string csproj, ProjectSettings settings)
    {
        var generated = FindGenerated(csproj);
        if (generated is not null)
        {
            return CSharpSyntaxTree.ParseText(File.ReadAllText(generated), path: generated);
        }

        if (!settings.ImplicitUsings && settings.Usings.Count == 0)
        {
            return null;
        }

        var path = Path.Combine(
            Path.GetDirectoryName(csproj) ?? string.Empty,
            ObjDir,
            Path.GetFileNameWithoutExtension(csproj) + SynthesizedSuffix);
        return CSharpSyntaxTree.ParseText(Synthesize(settings), path: path);
    }

    // The generated file is named after the csproj file, not AssemblyName. It is trusted only when it
    // is at least as new as the csproj, so a <Using> added since the last build is still seen.
    private static string? FindGenerated(string csproj)
    {
        var obj = Path.Combine(Path.GetDirectoryName(csproj) ?? string.Empty, ObjDir);
        if (!Directory.Exists(obj))
        {
            return null;
        }

        var name = Path.GetFileNameWithoutExtension(csproj) + GeneratedSuffix;
        var newest = Directory.EnumerateFiles(obj, name, SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        return newest is not null && File.GetLastWriteTimeUtc(newest) >= File.GetLastWriteTimeUtc(csproj)
            ? newest
            : null;
    }

    internal static string Synthesize(ProjectSettings settings)
    {
        var usings = new List<ProjectUsing>();
        if (settings.ImplicitUsings)
        {
            usings.AddRange(DefaultsFor(settings.Sdk).Select(name => new ProjectUsing(name, false, null, false)));
        }

        foreach (var item in settings.Usings)
        {
            if (item.Remove)
            {
                usings.RemoveAll(existing => existing.Name == item.Name);
                continue;
            }

            usings.Add(item);
        }

        return string.Concat(usings.Distinct().Select(item => $"{Directive(item)}\n"));
    }

    private static IEnumerable<string> DefaultsFor(string sdk) =>
        sdk switch
        {
            Sdk => SdkUsings,
            WebSdk => [.. SdkUsings, .. WebUsings],
            WorkerSdk => [.. SdkUsings, .. ExtensionsUsings],
            _ => [],
        };

    private static string Directive(ProjectUsing item)
    {
        if (item.Alias is not null)
        {
            return $"global using {item.Alias} = global::{item.Name};";
        }

        return item.Static ? $"global using static global::{item.Name};" : $"global using global::{item.Name};";
    }
}

using System.ComponentModel;
using IOFile = System.IO.File;
using System.Diagnostics;

namespace Qlcheck.Languages.CSharp.Checks.Coverage;

internal static class CoverageRunner
{
    private const string DotnetFile = "dotnet";

    private const string ExecCommand = "exec";

    private const string BuildCommand = "build";

    private const string TestCommand = "test";

    private const string NoRestore = "--no-restore";

    private const string NoBuild = "--no-build";

    private const string NoLogo = "--nologo";

    private const string TargetOption = "--target";

    private const string TargetArgsOption = "--targetargs";

    private const string OutputOption = "--output";

    private const string FormatOption = "--format";

    private const string LcovFormat = "lcov";

    private const string IncludeOption = "--include";

    private const string VerbosityOption = "--verbosity";

    private const string QuietVerbosity = "Quiet";

    private const string SingleHitOption = "--single-hit";

    private const string SkipAutoPropsOption = "--skipautoprops";

    private const string ExcludeAttributeOption = "--exclude-by-attribute";

    private const string ExcludeFromCodeCoverage = "ExcludeFromCodeCoverage";

    private const string GeneratedCode = "GeneratedCode";

    private const string CompilerGenerated = "CompilerGenerated";

    private const string BinDirectory = "bin";

    private const string Configuration = "Debug";

    private const string DllSuffix = ".dll";

    private const string RefDirectory = "ref";

    private const string ObjDirectory = "obj";

    private const string AssetsFile = "project.assets.json";

    private const string InfoSuffix = ".info";

    private const string CacheFolder = "qlcheck-coverage";

    private const int Success = 0;

    private const int TestsFailed = 1;

    private const int StderrTailLines = 40;

    public static string Collect(string testCsproj, string productCsproj, string include)
    {
        CsprojReader.RequireSingleFramework(testCsproj);
        CsprojReader.RequireSingleFramework(productCsproj);
        Build(testCsproj);
        var testDll = FindAssembly(testCsproj);
        var productDll = FindAssembly(productCsproj);
        if (CoverageCache.TryRead(testDll, productDll, include, out var cached))
        {
            return cached;
        }

        var lcov = RunCoverlet(testDll, testCsproj, include);
        CoverageCache.Write(testDll, productDll, include, lcov);
        return lcov;
    }

    private static void Build(string testCsproj)
    {
        var args = new List<string> { BuildCommand, testCsproj, NoLogo };
        var assets = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(testCsproj))!, ObjDirectory, AssetsFile);
        if (IOFile.Exists(assets))
        {
            args.Add(NoRestore);
        }

        var code = Run(DotnetFile, args, Path.GetDirectoryName(Path.GetFullPath(testCsproj))!, out var stdout, out var stderr);
        if (code == Success)
        {
            return;
        }

        throw new InvalidOperationException($"Build failed for {testCsproj}.{Environment.NewLine}{Tail(stdout)}{Environment.NewLine}{Tail(stderr)}");
    }

    private static string RunCoverlet(string testDll, string testCsproj, string include)
    {
        var output = Path.Combine(Path.GetTempPath(), CacheFolder, $"{Guid.NewGuid():N}{InfoSuffix}");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        try
        {
            var targetArgs = $"{TestCommand} \"{testCsproj}\" {NoBuild} {NoRestore} {NoLogo}";
            var args = new List<string>
            {
                ExecCommand,
                CoverletTool.Dll(),
                testDll,
                TargetOption,
                DotnetFile,
                TargetArgsOption,
                targetArgs,
                OutputOption,
                output,
                FormatOption,
                LcovFormat,
                IncludeOption,
                include,
                VerbosityOption,
                QuietVerbosity,
                SingleHitOption,
                SkipAutoPropsOption,
                ExcludeAttributeOption,
                ExcludeFromCodeCoverage,
                ExcludeAttributeOption,
                GeneratedCode,
                ExcludeAttributeOption,
                CompilerGenerated,
            };
            var code = Run(
                DotnetFile,
                args,
                Path.GetDirectoryName(Path.GetFullPath(testCsproj))!,
                out var stdout,
                out var stderr);
            if (code == TestsFailed)
            {
                throw new InvalidOperationException(
                    $"Tests failed for {testCsproj}.{Environment.NewLine}{Tail(stdout)}{Environment.NewLine}{Tail(stderr)}");
            }

            if (code != Success)
            {
                throw new InvalidOperationException(
                    $"Coverage failed for {testCsproj}.{Environment.NewLine}{Tail(stdout)}{Environment.NewLine}{Tail(stderr)}");
            }

            if (!IOFile.Exists(output))
            {
                throw new InvalidOperationException($"Coverage failed for {testCsproj}. No lcov was written.");
            }

            return IOFile.ReadAllText(output);
        }
        finally
        {
            if (IOFile.Exists(output))
            {
                IOFile.Delete(output);
            }
        }
    }

    private static string FindAssembly(string csproj)
    {
        var info = CsprojReader.Read(csproj);
        var dir = Path.GetDirectoryName(Path.GetFullPath(csproj))!;
        if (info.TargetFramework is not null)
        {
            var expected = Path.Combine(
                dir,
                BinDirectory,
                Configuration,
                info.TargetFramework,
                $"{info.AssemblyName}{DllSuffix}");
            if (IOFile.Exists(expected))
            {
                return expected;
            }
        }

        var bin = Path.Combine(dir, BinDirectory);
        if (!Directory.Exists(bin))
        {
            throw new InvalidOperationException($"Assembly {info.AssemblyName} was not found for {csproj}.");
        }

        var name = $"{info.AssemblyName}{DllSuffix}";
        var matches = new List<string>();
        foreach (var file in Directory.EnumerateFiles(bin, name, SearchOption.AllDirectories))
        {
            if (!IsRef(file))
            {
                matches.Add(file);
            }
        }

        if (matches.Count == 0)
        {
            throw new InvalidOperationException($"Assembly {info.AssemblyName} was not found for {csproj}.");
        }

        if (matches.Count == 1)
        {
            return matches[0];
        }

        var needle = $"{Path.DirectorySeparatorChar}{Configuration}{Path.DirectorySeparatorChar}";
        var debug = matches.Where(match => match.Contains(needle, StringComparison.OrdinalIgnoreCase)).ToList();
        if (debug.Count == 1)
        {
            return debug[0];
        }

        throw new InvalidOperationException($"{csproj} has multiple target frameworks.");
    }

    private static bool IsRef(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var part in parts)
        {
            if (part.Equals(RefDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int Run(
        string fileName,
        IReadOnlyList<string> args,
        string workingDirectory,
        out string stdout,
        out string stderr)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = info };
        try
        {
            process.Start();
        }
        catch (Win32Exception)
        {
            throw new InvalidOperationException("dotnet was not found on PATH.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        stdout = stdoutTask.GetAwaiter().GetResult();
        stderr = stderrTask.GetAwaiter().GetResult();
        return process.ExitCode;
    }

    private static string Tail(string text)
    {
        if (text.Length == 0)
        {
            return string.Empty;
        }

        var lines = text.ReplaceLineEndings("\n").Split('\n');
        if (lines.Length <= StderrTailLines)
        {
            return text.Trim();
        }

        return string.Join('\n', lines[^StderrTailLines..]);
    }
}

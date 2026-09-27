using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Qlcheck.Tests;

public class ProjectReferencesTests
{
    [Fact]
    public void Load_is_empty_when_assets_file_is_missing_package_data()
    {
        var dir = CreateProjectDir();
        try
        {
            WriteAssets(dir, "{}");

            Assert.Empty(ProjectReferences.Load(dir.Csproj));
        }
        finally
        {
            dir.Root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Load_is_empty_when_package_folders_is_empty()
    {
        var dir = CreateProjectDir();
        try
        {
            WriteAssets(dir, """
                {
                  "packageFolders": {},
                  "libraries": {}
                }
                """);

            Assert.Empty(ProjectReferences.Load(dir.Csproj));
        }
        finally
        {
            dir.Root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Load_skips_target_entries_that_have_no_matching_library()
    {
        var dir = CreateProjectDir();
        try
        {
            WriteAssets(dir, $$"""
                {
                  "packageFolders": { "{{dir.Packages}}": {} },
                  "libraries": {},
                  "targets": {
                    "net10.0": {
                      "Missing/1.0.0": { "compile": { "lib/net10.0/Missing.dll": {} } }
                    }
                  }
                }
                """);

            Assert.Empty(ProjectReferences.Load(dir.Csproj));
        }
        finally
        {
            dir.Root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Load_skips_libraries_that_are_not_packages()
    {
        var dir = CreateProjectDir();
        try
        {
            WriteAssets(dir, $$"""
                {
                  "packageFolders": { "{{dir.Packages}}": {} },
                  "libraries": {
                    "Project/1.0.0": { "type": "project" }
                  },
                  "targets": {
                    "net10.0": {
                      "Project/1.0.0": { "compile": { "lib/net10.0/Project.dll": {} } }
                    }
                  }
                }
                """);

            Assert.Empty(ProjectReferences.Load(dir.Csproj));
        }
        finally
        {
            dir.Root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Load_skips_non_dll_compile_entries_and_missing_files()
    {
        var dir = CreateProjectDir();
        try
        {
            WriteAssets(dir, $$"""
                {
                  "packageFolders": { "{{dir.Packages}}": {} },
                  "libraries": {
                    "Sample/1.0.0": { "type": "package", "path": "sample/1.0.0" }
                  },
                  "targets": {
                    "net10.0": {
                      "Sample/1.0.0": {
                        "compile": {
                          "lib/net10.0/Sample.xml": {},
                          "lib/net10.0/Missing.dll": {}
                        }
                      }
                    }
                  }
                }
                """);

            Assert.Empty(ProjectReferences.Load(dir.Csproj));
        }
        finally
        {
            dir.Root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Load_adds_a_reference_for_a_real_package_dll()
    {
        var dir = CreateProjectDir();
        try
        {
            var libDir = Directory.CreateDirectory(Path.Combine(dir.Packages, "sample", "1.0.0", "lib", "net10.0"));
            EmitAssembly(Path.Combine(libDir.FullName, "Sample.dll"));
            WriteAssets(dir, $$"""
                {
                  "packageFolders": { "{{dir.Packages}}": {} },
                  "libraries": {
                    "Sample/1.0.0": { "type": "package", "path": "sample/1.0.0" }
                  },
                  "targets": {
                    "net10.0": {
                      "Sample/1.0.0": { "compile": { "lib/net10.0/Sample.dll": {} } }
                    }
                  }
                }
                """);

            Assert.Single(ProjectReferences.Load(dir.Csproj));
        }
        finally
        {
            dir.Root.Delete(recursive: true);
        }
    }

    private static ProjectDir CreateProjectDir()
    {
        var root = Directory.CreateTempSubdirectory("qlcheck_projrefs_");
        var csproj = Path.Combine(root.FullName, "Sample.csproj");
        File.WriteAllText(csproj, "<Project></Project>");
        var packages = Path.Combine(root.FullName, "packages");
        Directory.CreateDirectory(packages);
        return new ProjectDir(root, csproj, packages);
    }

    private static void WriteAssets(ProjectDir dir, string json)
    {
        var objDir = Directory.CreateDirectory(Path.Combine(dir.Root.FullName, "obj"));
        File.WriteAllText(Path.Combine(objDir.FullName, "project.assets.json"), json);
    }

    private static void EmitAssembly(string path)
    {
        var tree = CSharpSyntaxTree.ParseText("public class Marker { }");
        var compilation = CSharpCompilation.Create(
            "Sample",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var result = compilation.Emit(path);
        Assert.True(result.Success);
    }

    private sealed record ProjectDir(DirectoryInfo Root, string Csproj, string Packages);
}

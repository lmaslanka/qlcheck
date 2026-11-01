using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

// Runs the architecture engine over in-memory sources F0.cs, F1.cs, ... in a temporary folder whose
// .qlcheck_architecture holds `constraints` (no file when it is null).
internal static class ArchitectureFixture
{
    public static IReadOnlyList<Finding> Run(string? constraints, string check, params string[] sources)
    {
        var dir = Directory.CreateTempSubdirectory("qlcheck_arch_fixture_").FullName;
        try
        {
            if (constraints is not null)
            {
                File.WriteAllText(Path.Combine(dir, ArchitectureFiles.FileName), $"{constraints}\n");
            }

            var files = sources.Select((source, index) => (Full: Path.Combine(dir, $"F{index}.cs"), Source: source)).ToList();
            files.ForEach(file => File.WriteAllText(file.Full, file.Source));
            var loaded = ArchitectureFiles.Load([dir], files.Select(file => file.Full));
            var trees = files.Select(file => CSharpTrees.Parse(new SourceFile(Path.GetFileName(file.Full), file.Source))).ToList();
            var compilation = CSharpCompilations.Create("architecture", trees);
            var dependencies = files.Zip(trees)
                .Where(pair => loaded.For(pair.First.Full) is not null)
                .SelectMany(pair => DependencyExtractor.Extract(compilation.GetSemanticModel(pair.Second), pair.Second.FilePath)
                    .Select(dependency => new GovernedDependency(loaded.For(pair.First.Full)!, dependency)))
                .ToList();
            var messages = new Dictionary<string, string>(StringComparer.Ordinal) { [check] = "fallback" };
            return [.. ArchitectureEngine.Apply(dependencies, new HashSet<string>(StringComparer.Ordinal) { check }, messages)];
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

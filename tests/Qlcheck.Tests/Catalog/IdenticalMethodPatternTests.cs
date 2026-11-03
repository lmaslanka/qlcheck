using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class IdenticalMethodPatternTests
{
    private const string Duplicate = "duplicate-code";

    private const string Identical = "identical-method";

    private const string Daily = "Daily";

    private const string Weekly = "Weekly";

    private const string Path = "src/A.cs";

    private const string Body = """
                var total = 0;
                foreach (var line in lines)
                {
                    total += line.Length;
                }

                Console.WriteLine("lines " + lines.Count);
                Console.WriteLine("characters " + total);
                Console.WriteLine("average " + total / Math.Max(1, lines.Count));
        """;

    [Fact]
    public void Two_methods_with_the_same_whole_body_are_identical_methods_only()
    {
        var findings = Run(Type(Method(Daily, Body) + Method(Weekly, Body)));

        Assert.Equal([Identical, Identical], findings.Select(finding => finding.Check));
        Assert.Contains("`Daily`", findings[1].Message, StringComparison.Ordinal);
        Assert.Contains("make one call the other", findings[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Renamed_whole_bodies_are_told_to_extract_a_shared_method()
    {
        var renamed = Body.Replace("total", "sum", StringComparison.Ordinal);

        var findings = Run(Type(Method(Daily, Body) + Method(Weekly, renamed)));

        Assert.Equal([Identical, Identical], findings.Select(finding => finding.Check));
        Assert.All(findings, finding => Assert.EndsWith("extract one shared method whose parameters are the parts that differ.", finding.Message, StringComparison.Ordinal));
        Assert.DoesNotContain(findings, finding => finding.Message.Contains("make one call the other", StringComparison.Ordinal));
    }

    [Fact]
    public void A_copy_that_is_part_of_a_body_is_duplicate_code()
    {
        var findings = Run(Type(Method(Daily, Body) + Method(Weekly, $"        Console.WriteLine();\n{Body}")));

        Assert.Equal([Duplicate, Duplicate], findings.Select(finding => finding.Check));
    }

    [Fact]
    public void Accessor_and_local_function_bodies_count_as_whole_bodies()
    {
        var accessor = $"    public int Total\n    {{\n        get\n        {{\n            var lines = new System.Collections.Generic.List<string>();\n{Body}\n            return 0;\n        }}\n    }}\n";
        var other = $"    public int Other\n    {{\n        get\n        {{\n            var lines = new System.Collections.Generic.List<string>();\n{Body}\n            return 0;\n        }}\n    }}\n";

        Assert.Equal([Identical, Identical], Run(Type(accessor + other)).Select(finding => finding.Check));
    }

    private static string Method(string name, string body) =>
        $"    public void {name}(System.Collections.Generic.List<string> lines)\n    {{\n{body}\n    }}\n";

    private static string Type(string members) => $"using System;\n\npublic class Holder\n{{\n{members}}}\n";

    private static List<Finding> Run(string source)
    {
        var file = new SourceSetFile(Path, CSharpTrees.Parse(new SourceFile(Path, source)));
        var ctx = new SourceSetContext([file], new Dictionary<string, string>(StringComparer.Ordinal) { [Duplicate] = "d", [Identical] = "Methods should not have identical implementations." });
        SourceSetEngine.Apply(ctx, new HashSet<string>(StringComparer.Ordinal) { Duplicate, Identical });
        return [.. ctx.Findings];
    }
}

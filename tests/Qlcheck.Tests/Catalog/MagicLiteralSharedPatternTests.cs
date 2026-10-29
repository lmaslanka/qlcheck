using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class MagicLiteralSharedPatternTests
{
    private const string Id = "magic-literal-shared";

    private const string FileA = "A.cs";

    private const string FileB = "B.cs";

    private const int Two = 2;

    private const string Order = """
        namespace Sample
        {
            class Order { public string Status { get; set; } public string Note { get; set; } }
            class Door { public string State { get; set; } }
            static class OrderStatus { public const string Shipped = "shipped"; }
        }
        """;

    private static IReadOnlyList<Finding> Run(params (string Path, string Source)[] files) =>
        RunWithIncluded(files.Select(file => file.Path), files);

    private static IReadOnlyList<Finding> RunWithIncluded(
        IEnumerable<string> included,
        params (string Path, string Source)[] files)
    {
        var trees = files.Select(file => CSharpTrees.Parse(new SourceFile(file.Path, file.Source))).ToList();
        var compilation = CSharpCompilations.Create("fixture", trees);
        var messages = new Dictionary<string, string>(StringComparer.Ordinal) { [Id] = "message" };
        var ctx = new CompilationContext(compilation, included.ToHashSet(StringComparer.OrdinalIgnoreCase), messages);
        MagicLiteralSharedPattern.Apply(ctx, Id);
        return ctx.Findings;
    }

    [Fact]
    public void Reports_literal_when_a_const_is_already_used_for_the_same_target()
    {
        var a = """
            namespace Sample
            {
                class A { bool M(Order o) => o.Status == OrderStatus.Shipped; }
            }
            """;
        var b = """
            namespace Sample
            {
                class B { void M(Order o) => o.Status = "shipped"; }
            }
            """;

        var finding = Assert.Single(Run(("Order.cs", Order), (FileA, a), (FileB, b)));
        Assert.Equal(FileB, finding.File);
        Assert.Equal("OrderStatus.Shipped", finding.Replacement);
        Assert.Equal(
            MagicLiteralSharedPattern.ExistingMessage("Magic string \"shipped\"", "OrderStatus.Shipped", "Order.Status"),
            finding.Message);
    }

    [Fact]
    public void Ignores_const_value_used_for_a_different_target()
    {
        var a = """
            namespace Sample
            {
                class A { bool M(Order o) => o.Status == OrderStatus.Shipped; }
            }
            """;
        var b = """
            namespace Sample
            {
                class B { void M(Order o) => o.Note = "shipped"; }
            }
            """;

        Assert.Empty(Run(("Order.cs", Order), (FileA, a), (FileB, b)));
    }

    [Fact]
    public void Reports_same_literal_for_same_target_in_two_files()
    {
        var a = """
            namespace Sample
            {
                class A { bool M(Order o) => o.Status == "open"; }
            }
            """;
        var b = """
            namespace Sample
            {
                class B { void M(Order o) => o.Status = "open"; }
            }
            """;

        var findings = Run(("Order.cs", Order), (FileA, a), (FileB, b));
        Assert.Equal(Two, findings.Count);
        var inA = Assert.Single(findings, finding => finding.File == FileA);
        Assert.Equal(
            MagicLiteralSharedPattern.SharedMessage("Magic string \"open\"", "Order.Status", Two, FileB),
            inA.Message);
        Assert.Null(inA.Replacement);
    }

    [Fact]
    public void Ignores_same_literal_for_different_targets()
    {
        var a = """
            namespace Sample
            {
                class A { bool M(Order o) => o.Status == "open"; }
            }
            """;
        var b = """
            namespace Sample
            {
                class B { bool M(Door d) => d.State == "open"; }
            }
            """;

        Assert.Empty(Run(("Order.cs", Order), (FileA, a), (FileB, b)));
    }

    [Fact]
    public void Ignores_repeats_within_one_file()
    {
        var a = """
            namespace Sample
            {
                class A
                {
                    bool M(Order o) => o.Status == "open";
                    void N(Order o) => o.Status = "open";
                }
            }
            """;

        Assert.Empty(Run(("Order.cs", Order), (FileA, a)));
    }

    [Fact]
    public void Counts_excluded_files_but_reports_only_included_ones()
    {
        var a = """
            namespace Sample
            {
                class A { bool M(Order o) => o.Status == "open"; }
            }
            """;
        var b = """
            namespace Sample
            {
                class B { void M(Order o) => o.Status = "open"; }
            }
            """;

        var finding = Assert.Single(RunWithIncluded([FileA], ("Order.cs", Order), (FileA, a), (FileB, b)));
        Assert.Equal(FileA, finding.File);
    }

    [Fact]
    public void Matches_method_parameter_targets()
    {
        var api = """
            namespace Sample
            {
                class Config { public string Get(string key) => key; public string Other(string key) => key; }
            }
            """;
        var a = """
            namespace Sample
            {
                class A { string M(Config c) => c.Get("timeout"); }
            }
            """;
        var b = """
            namespace Sample
            {
                class B { string M(Config c) => c.Get(key: "timeout"); string N(Config c) => c.Other("timeout"); }
            }
            """;

        var findings = Run(("Config.cs", api), (FileA, a), (FileB, b));
        Assert.Equal(Two, findings.Count);
        Assert.All(findings, finding => Assert.Contains("Config.Get(string)", finding.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Matches_indexer_targets()
    {
        var a = """
            namespace Sample
            {
                class A { int M(System.Collections.Generic.Dictionary<string, int> d) => d["retries"]; }
            }
            """;
        var b = """
            namespace Sample
            {
                class B { void M(System.Collections.Generic.Dictionary<string, int> d) => d["retries"] = 3; }
            }
            """;

        Assert.Equal(Two, Run((FileA, a), (FileB, b)).Count);
    }

    [Fact]
    public void Matches_pattern_and_case_targets_against_existing_const()
    {
        var a = """
            namespace Sample
            {
                class A
                {
                    int M(Order o)
                    {
                        switch (o.Status)
                        {
                            case OrderStatus.Shipped: return 2;
                            default: return 0;
                        }
                    }
                }
            }
            """;
        var b = """
            namespace Sample
            {
                class B
                {
                    bool M(Order o) => o.Status is "shipped";
                    int N(Order o) => o.Status switch { "shipped" => 2, _ => 0 };
                }
            }
            """;

        var findings = Run(("Order.cs", Order), (FileA, a), (FileB, b));
        Assert.Equal(Two, findings.Count);
        Assert.All(findings, finding => Assert.Equal("OrderStatus.Shipped", finding.Replacement));
    }

    [Fact]
    public void Matches_negative_numbers_as_one_value()
    {
        var api = """
            namespace Sample
            {
                class Box { public int Offset { get; set; } public const int Back = -5; }
            }
            """;
        var a = """
            namespace Sample
            {
                class A { bool M(Box b) => b.Offset == Box.Back; void N(Box b) => b.Offset = -5; }
            }
            """;

        var finding = Assert.Single(Run(("Box.cs", api), (FileA, a)));
        Assert.Equal("Box.Back", finding.Replacement);
        Assert.Contains("Magic number -5", finding.Message, StringComparison.Ordinal);
    }
}

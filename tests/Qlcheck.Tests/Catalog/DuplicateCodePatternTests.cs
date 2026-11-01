using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class DuplicateCodePatternTests
{
    private const string Id = "duplicate-code";

    private const string FileA = "src/A.cs";

    private const string FileB = "src/B.cs";

    private const string First = "First";

    private const string Second = "Second";

    private const int TwoCopies = 2;

    private const int ThreeCopies = 3;

    private const int FiveCopies = 5;

    // Six statements and 58 tokens: a copy of this is duplicate code.
    private const string Block = """
                var total = 0;
                foreach (var item in items)
                {
                    total += item.Length * 2;
                }

                Console.WriteLine("total " + total);
                Console.WriteLine("count " + items.Count);
                Console.WriteLine("done at " + DateTime.Now.Ticks);
                return total;
        """;

    [Fact]
    public void Two_files_with_the_same_block_give_one_Finding_per_copy_naming_the_other()
    {
        var findings = Run((FileA, Member(First, Block)), (FileB, Member(Second, Block)));

        Assert.Equal([FileA, FileB], findings.Select(finding => finding.File));
        Assert.Contains($"{FileB}:", findings[0].Message, StringComparison.Ordinal);
        Assert.Contains($"{FileA}:", findings[1].Message, StringComparison.Ordinal);
        Assert.All(findings, finding => Assert.Equal(Id, finding.Check));
    }

    [Fact]
    public void A_consistently_renamed_copy_is_duplicate_code()
    {
        var renamed = Block.Replace("var item in", "var entry in", StringComparison.Ordinal).Replace("item.Length", "entry.Length", StringComparison.Ordinal);

        Assert.Equal(TwoCopies, Run((FileA, Member(First, Block)), (FileB, Member(Second, renamed))).Count);
    }

    [Fact]
    public void An_inconsistent_rename_is_not()
    {
        // `total` maps to `sum` in the declaration and to `total` everywhere else.
        var inconsistent = Block.Replace("var total = 0;", "var sum = 0;", StringComparison.Ordinal);

        Assert.Empty(Run((FileA, Member(First, Block)), (FileB, Member(Second, inconsistent))));
    }

    [Fact]
    public void A_different_literal_is_not()
    {
        var changed = Block.Replace("item.Length * 2", "item.Length * 3", StringComparison.Ordinal);

        Assert.Empty(Run((FileA, Member(First, Block)), (FileB, Member(Second, changed))));
    }

    [Fact]
    public void A_block_under_the_thresholds_is_not()
    {
        const string Small = """
                    var total = 0;
                    total += items.Count;
                    return total;
            """;

        Assert.Empty(Run((FileA, Member(First, Small)), (FileB, Member(Second, Small))));
    }

    [Fact]
    public void A_copy_within_one_file_is_reported()
    {
        var source = Type(Method(First, Block) + Method(Second, Block));

        Assert.Equal(TwoCopies, Run((FileA, source)).Count);
    }

    [Fact]
    public void Generated_files_are_neither_reported_nor_named()
    {
        var findings = Run((FileA, Member(First, Block)), ("src/A.g.cs", Member(Second, Block)));

        Assert.Empty(findings);
    }

    [Fact]
    public void Member_lists_records_tables_and_switch_expressions_are_not_units()
    {
        const string Boilerplate = """
            namespace Sample
            {
                public record Person(string First, string Last, int Age, string City, string Country);
                public class Dto { public string First { get; set; } = ""; public string Last { get; set; } = ""; public int Age { get; set; } public string City { get; set; } = ""; }
                public static class Table
                {
                    public static readonly string[] Rows = ["alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta"];
                    public static int Code(string name) => name switch { "alpha" => 1, "beta" => 2, "gamma" => 3, "delta" => 4, _ => 0 };
                }
            }
            """;

        Assert.Empty(Run((FileA, Boilerplate), (FileB, Boilerplate.Replace("Sample", "Other", StringComparison.Ordinal))));
    }

    [Fact]
    public void A_run_of_one_repeated_statement_shape_is_not()
    {
        const string Asserts = """
                    Check(a.First, b.First);
                    Check(a.Last, b.Last);
                    Check(a.Age, b.Age);
                    Check(a.City, b.City);
                    Check(a.Country, b.Country);
                    Check(a.Zip, b.Zip);
                    Check(a.Phone, b.Phone);
                    Check(a.Mail, b.Mail);
            """;

        Assert.Empty(Run((FileA, Member(First, Asserts)), (FileB, Member(Second, Asserts))));
    }

    [Fact]
    public void A_nested_block_inside_a_reported_copy_is_not_reported_again()
    {
        var outer = Block + "\n        if (items.Count > 1)\n        {\n" + Block.Replace("return total;", "Console.WriteLine();", StringComparison.Ordinal) + "\n        }";

        var findings = Run((FileA, Member(First, outer)), (FileB, Member(Second, outer)));

        Assert.Equal(TwoCopies, findings.Count);
    }

    [Fact]
    public void Three_copies_give_three_Findings_each_naming_the_two_others()
    {
        var findings = Run((FileA, Member(First, Block)), (FileB, Member(Second, Block)), ("src/C.cs", Member("Third", Block)));

        Assert.Equal(ThreeCopies, findings.Count);
        Assert.All(findings, finding => Assert.Equal(TwoCopies, finding.Message.Split("src/").Length - 1));
    }

    [Fact]
    public void Five_copies_name_three_others_and_count_the_rest()
    {
        var files = Enumerable.Range(0, FiveCopies).Select(index => ($"src/F{index}.cs", Member($"M{index}", Block))).ToArray();

        var findings = Run(files);

        Assert.Equal(FiveCopies, findings.Count);
        Assert.All(findings, finding => Assert.Contains("and 1 more place", finding.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void The_result_does_not_depend_on_the_order_of_the_files()
    {
        var files = new[] { (FileA, Member(First, Block)), (FileB, Member(Second, Block)), ("src/C.cs", Member("Third", Block)) };

        Assert.Equal(Run(files).Select(finding => finding.Id), Run([.. files.Reverse()]).Select(finding => finding.Id));
    }

    [Fact]
    public void Thousands_of_identical_statements_stay_bounded()
    {
        var many = string.Concat(Enumerable.Repeat("        Console.WriteLine(\"same line of output, again and again\");\n", 5000));

        Assert.Empty(Run((FileA, Member(First, many)), (FileB, Member(Second, many))));
    }

    private static string Member(string name, string body) => Type(Method(name, body));

    // An extra statement first, so no copy is a whole method body (that is identical-method).
    private static string Method(string name, string body) =>
        $"    public int {name}(System.Collections.Generic.List<string> items)\n    {{\n        Console.WriteLine(\"{name}\");\n{body}\n    }}\n";

    private static string Type(string members) => $"using System;\n\npublic class Holder\n{{\n{members}}}\n";

    private static List<Finding> Run(params (string Path, string Source)[] files)
    {
        var sources = files.Select(file => new SourceSetFile(file.Path, CSharpTrees.Parse(new SourceFile(file.Path, file.Source)))).ToList();
        var ctx = new SourceSetContext(sources, new Dictionary<string, string>(StringComparer.Ordinal) { [Id] = "message" });
        DuplicateCodePattern.Apply(ctx, Id);
        return [.. ctx.Findings];
    }
}

using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class MagicLiteralPatternTests
{
    private const string FortyTwo = "42";

    private const string Pending = "pending";

    private const int Twice = 2;

    private const int Thrice = 3;

    private static IReadOnlyList<Finding> Run(string source)
    {
        var ctx = MatchFixtures.Context(source);
        MagicLiteralPattern.Apply(ctx, MatchFixtures.Id);
        return ctx.Findings;
    }

    [Fact]
    public void Reports_number_repeated_in_file()
    {
        var source = """
            class C
            {
                int M() => 42;
                int N() => 42;
            }
            """;

        var findings = Run(source);
        Assert.Equal(Twice, findings.Count);
        Assert.All(findings, finding => Assert.Equal(MatchFixtures.Id, finding.Check));
        Assert.All(findings, finding => Assert.Equal(MagicLiteralPattern.NumberMessage(FortyTwo, Twice), finding.Message));
        Assert.All(findings, finding => Assert.Null(finding.Replacement));
    }

    [Fact]
    public void Ignores_number_used_once()
    {
        var source = """
            class C
            {
                int M()
                {
                    return 42;
                }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Ignores_string_used_once()
    {
        var source = """
            class C
            {
                string M() => "pending";
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Reports_string_repeated_in_file_with_count()
    {
        var source = """
            class C
            {
                string M() => "pending";
                string N() => "pending";
                bool O(string s) => s == "pending";
            }
            """;

        var findings = Run(source);
        Assert.Equal(Thrice, findings.Count);
        Assert.All(findings, finding => Assert.Equal(MagicLiteralPattern.StringMessage(Pending, Thrice), finding.Message));
    }

    [Fact]
    public void Does_not_group_values_of_different_types()
    {
        var source = """
            class C
            {
                int M() => 5;
                double N() => 5.0;
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Allows_zero_and_one()
    {
        var source = """
            class C
            {
                int M() => 0 + 1;
                int N() => 0 + 1;
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Skips_const_and_enum_members()
    {
        var source = """
            class C
            {
                const int Max = 42;
                const int Min = -42;
                enum Kind { A = 42 }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Skips_const_string_and_empty_string()
    {
        var source = """
            class C
            {
                const string Name = "pending";
                const string Other = "pending";
                string M() => "";
                string N() => "";
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Skips_multi_word_messages()
    {
        var source = """
            class C
            {
                string M() => "User not found";
                string N() => "User not found";
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Skips_long_strings()
    {
        var source = """
            class C
            {
                string M() => "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopq";
                string N() => "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopq";
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Skips_sql_fragments()
    {
        var source = """
            class C
            {
                string M() => "NULLIF(";
                string N() => "NULLIF(";
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Skips_dotted_names()
    {
        var source = """
            class C
            {
                string M() => "letter.world";
                string N() => "letter.world";
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Skips_exception_messages()
    {
        var source = """
            class C
            {
                void M() => throw new InvalidOperationException("gone");
                void N() => throw new InvalidOperationException("gone");
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Skips_attribute_arguments()
    {
        var source = """
            [System.Obsolete("gone")]
            class C { }
            [System.Obsolete("gone")]
            class D { }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Skips_strings_in_array_initializer()
    {
        var source = """
            class C
            {
                string[] M() => new string[] { "Chiapas", "Chiapas" };
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Skips_strings_in_collection_initializer()
    {
        var source = """
            class C
            {
                System.Collections.Generic.List<string> M() => new System.Collections.Generic.List<string> { "Chiapas", "Chiapas" };
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Skips_strings_in_collection_expression()
    {
        var source = """
            class C
            {
                string[] M() => ["Chiapas", "Chiapas"];
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Reports_string_in_object_initializer()
    {
        var source = """
            class Foo { public string Name; }
            class C
            {
                Foo M() => new Foo { Name = "pending" };
                Foo N() => new Foo { Name = "pending" };
            }
            """;

        var findings = Run(source);
        Assert.Equal(Twice, findings.Count);
        Assert.All(findings, finding => Assert.Equal(MagicLiteralPattern.StringMessage(Pending, Twice), finding.Message));
    }

    [Fact]
    public void Reports_number_in_array_initializer()
    {
        var source = """
            class C
            {
                int[] M() => new int[] { 42, 42 };
            }
            """;

        Assert.Equal(Twice, Run(source).Count);
    }

    [Fact]
    public void Ignores_boolean_literal()
    {
        var source = """
            class C
            {
                bool M() => true;
                bool N() => true;
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Skips_string_thrown_from_a_generic_exception_type()
    {
        var source = """
            class MyException<T> : System.Exception { }
            class C
            {
                void M() => throw new MyException<int>("magicvalue");
                void N() => throw new MyException<int>("magicvalue");
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Reports_number_arguments_and_parameter_defaults()
    {
        var source = """
            class C
            {
                void M(int x = 42)
                {
                    Foo(42);
                    for (var i = 42; i < Bound; i++) { }
                }
            }
            """;

        var findings = Run(source);
        Assert.Equal(Thrice, findings.Count);
        Assert.All(findings, finding => Assert.Equal(MagicLiteralPattern.NumberMessage(FortyTwo, Thrice), finding.Message));
    }

    [Fact]
    public void Skips_local_const_number()
    {
        var source = """
            class C
            {
                void M()
                {
                    const int x = 42;
                    const int y = 42;
                }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Reports_magic_decimal_number()
    {
        var source = """
            class C
            {
                decimal M() => 2.5m;
                decimal N() => 2.5m;
            }
            """;

        var findings = Run(source);
        Assert.Equal(Twice, findings.Count);
        Assert.Equal(MagicLiteralPattern.NumberMessage("2.5m", Twice), findings[0].Message);
    }
}

namespace Qlcheck.Tests;

public class MagicLiteralCheckTests
{
    private static IReadOnlyList<Finding> Run(string source, string path = "Repo.cs")
    {
        ICheck check = new MagicLiteralCheck();
        return check.Run(new CheckContext([new SourceFile(path, source)]));
    }

    [Fact]
    public void Reports_magic_number_in_method_body()
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

        var finding = Assert.Single(Run(source));
        Assert.Equal("magic-literal", finding.Check);
        Assert.Equal(MagicLiteralCheck.NumberMessage("42"), finding.Message);
        Assert.Null(finding.Replacement);
    }

    [Fact]
    public void Allows_zero_and_one()
    {
        var source = """
            class C
            {
                int M()
                {
                    return 0 + 1;
                }
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
                enum Kind { A = 3 }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Reports_magic_string_in_method_body()
    {
        var source = """
            class C
            {
                string M()
                {
                    return "pending";
                }
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal("magic-literal", finding.Check);
        Assert.Equal(MagicLiteralCheck.StringMessage("pending"), finding.Message);
    }

    [Fact]
    public void Skips_const_string_and_empty_string()
    {
        var source = """
            class C
            {
                const string Name = "pending";
                string M() => "";
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
                void M()
                {
                    throw new InvalidOperationException("gone");
                }
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
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Skips_strings_in_array_initializer()
    {
        var source = """
            class C
            {
                string[] M() => new string[] { "Aguascalientes", "Chiapas" };
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
                System.Collections.Generic.List<string> M() => new System.Collections.Generic.List<string> { "Chiapas" };
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
                string[] M() => ["Chiapas"];
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
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal(MagicLiteralCheck.StringMessage("pending"), finding.Message);
    }

    [Fact]
    public void Reports_number_in_array_initializer()
    {
        var source = """
            class C
            {
                int[] M() => new int[] { 42 };
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal(MagicLiteralCheck.NumberMessage("42"), finding.Message);
    }
}

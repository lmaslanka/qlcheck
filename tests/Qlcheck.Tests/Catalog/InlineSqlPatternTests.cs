using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class InlineSqlPatternTests
{
    private const int SqlLine = 5;

    private const int SqlColumn = 19;

    private const string LiteralOne = "1";

    private static IReadOnlyList<Finding> Run(string source, string path = "Repo.cs")
    {
        var ctx = MatchFixtures.Context(source, path: path);
        InlineSqlPattern.Apply(ctx, MatchFixtures.Id);
        return ctx.Findings;
    }

    [Fact]
    public void Reports_one_hop_verbatim_sql_passed_to_query_async()
    {
        var source = """
            class C
            {
                void M()
                {
                    var sql = @"SELECT c.record_id
                                      ,c.name
                                      ,c.code_alpha2
                                  FROM countries As c
                                   WHERE c.is_deleted = false
                                  ORDER BY c.name ASC;";
                    connection.QueryAsync(sql);
                }
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal(MatchFixtures.Id, finding.Check);
        Assert.Equal("Repo.cs", finding.File);
        Assert.Equal(SqlLine, finding.Line);
        Assert.Equal(SqlColumn, finding.Column);
        Assert.Equal(InlineSqlPattern.LayoutMessage, finding.Message);
        Assert.Equal(
            "\"\"\"\n" +
            "            SELECT\n" +
            "                c.record_id,\n" +
            "                c.name,\n" +
            "                c.code_alpha2\n" +
            "            FROM countries AS c\n" +
            "            WHERE c.is_deleted = FALSE\n" +
            "            ORDER BY c.name ASC\n" +
            "            \"\"\"",
            finding.Replacement);
    }

    [Fact]
    public void Ignores_sql_not_passed_to_dapper_or_ado()
    {
        var source = """
            class C
            {
                void M()
                {
                    var sql = @"SELECT 1";
                    Console.WriteLine(sql);
                }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Reports_interpolated_sql_as_unformattable()
    {
        var source = """
            class C
            {
                void M()
                {
                    connection.QueryAsync($"SELECT {id}");
                }
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal(InlineSqlPattern.UnformattableMessage, finding.Message);
        Assert.Null(finding.Replacement);
    }

    [Fact]
    public void Reports_concatenated_sql_as_unformattable()
    {
        var source = """
            class C
            {
                void M()
                {
                    connection.Execute("SELECT " + "1");
                }
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal(InlineSqlPattern.UnformattableMessage, finding.Message);
        Assert.Null(finding.Replacement);
    }

    [Fact]
    public void Reports_command_text_assignment()
    {
        var source = """
            class C
            {
                void M()
                {
                    command.CommandText = "select 1";
                }
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal(InlineSqlPattern.LayoutMessage, finding.Message);
        Assert.Contains("SELECT", finding.Replacement);
        Assert.Contains(LiteralOne, finding.Replacement);
    }

    [Fact]
    public void Accepts_canonical_raw_string()
    {
        var source =
            "class C\n" +
            "{\n" +
            "    void M()\n" +
            "    {\n" +
            "        var sql = \"\"\"\n" +
            "            SELECT\n" +
            "                c.record_id,\n" +
            "                c.name\n" +
            "            FROM countries AS c\n" +
            "            WHERE c.is_deleted = FALSE\n" +
            "            ORDER BY c.name ASC\n" +
            "            \"\"\";\n" +
            "        connection.QueryAsync(sql);\n" +
            "    }\n" +
            "}\n";

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Reports_unparseable_sql_as_format_failed()
    {
        var source = """
            class C
            {
                void M()
                {
                    connection.QueryAsync("not a query");
                }
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.StartsWith(InlineSqlPattern.FormatFailedMessage, finding.Message);
        Assert.Null(finding.Replacement);
    }

    [Fact]
    public void Replacement_uses_qlfmt_output()
    {
        const string sql = "insert into table (col_a, col_b) select col_a, col_b from source where id = @auditEventId";
        var source = $$"""
            class C
            {
                void M()
                {
                    connection.QueryAsync("{{sql}}");
                }
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal(InlineSqlPattern.LayoutMessage, finding.Message);
        foreach (var line in QlFmt.Sql.Format(sql).ReplaceLineEndings("\n").Split('\n'))
        {
            Assert.Contains(line, finding.Replacement);
        }
    }

    [Fact]
    public void Formats_coalesce_without_throwing()
    {
        var source = """
            class C
            {
                void M()
                {
                    connection.QueryAsync("select coalesce(a, b) from t");
                }
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal(InlineSqlPattern.LayoutMessage, finding.Message);
        Assert.Contains("coalesce", finding.Replacement, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ignores_sql_argument_that_cannot_be_resolved()
    {
        var source = """
            class C
            {
                void M()
                {
                    connection.QueryAsync(GetSql());
                }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Ignores_identifier_referenced_from_an_expression_bodied_property()
    {
        var source = """
            class C
            {
                object P => connection.QueryAsync(sql);
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Resolves_variable_written_through_a_plain_assignment()
    {
        var source = """
            class C
            {
                void M()
                {
                    string sql;
                    other = "ignored";
                    sql = "select 1";
                    connection.QueryAsync(sql);
                }
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal(InlineSqlPattern.LayoutMessage, finding.Message);
    }

    [Fact]
    public void Reports_interpolated_sql_assigned_via_plain_assignment_as_unformattable()
    {
        var source = """
            class C
            {
                void M()
                {
                    string sql;
                    sql = $"SELECT {id}";
                    connection.QueryAsync(sql);
                }
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal(InlineSqlPattern.UnformattableMessage, finding.Message);
    }

    [Fact]
    public void Ignores_non_string_value_assigned_to_a_sql_looking_variable()
    {
        var source = """
            class C
            {
                void M()
                {
                    object sql;
                    sql = 42;
                    connection.QueryAsync(sql);
                }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Unwraps_parenthesized_sql_literal()
    {
        var source = """
            class C
            {
                void M()
                {
                    connection.QueryAsync(("select 1"));
                }
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal(InlineSqlPattern.LayoutMessage, finding.Message);
    }

    [Fact]
    public void Prefers_the_inner_shadowed_declaration_over_an_outer_one()
    {
        var source = """
            class C
            {
                void M()
                {
                    var sql = "outer value";
                    {
                        var sql = "select 1";
                        connection.QueryAsync(sql);
                    }
                }
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Contains("SELECT", finding.Replacement);
    }

    [Fact]
    public void Reports_sql_passed_to_a_command_constructor()
    {
        var source = """
            class C
            {
                void M()
                {
                    var cmd = new SqlCommand("select 1");
                }
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal(InlineSqlPattern.LayoutMessage, finding.Message);
    }

    [Fact]
    public void Recognizes_qualified_command_type_name()
    {
        var source = """
            class C
            {
                void M()
                {
                    var cmd = new System.Data.SqlClient.SqlCommand("select 1");
                }
            }
            """;

        var finding = Assert.Single(Run(source));
        Assert.Equal(InlineSqlPattern.LayoutMessage, finding.Message);
    }

    [Fact]
    public void Ignores_generic_type_creation_which_falls_back_to_type_text()
    {
        var source = """
            class C
            {
                void M()
                {
                    var list = new List<int>();
                }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Ignores_invocation_whose_callee_is_not_a_recognizable_name()
    {
        var source = """
            class C
            {
                void M()
                {
                    funcs[0]("select 1");
                }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void Recognizes_dapper_calls_through_conditional_unqualified_and_generic_invocations()
    {
        var source = """
            class C
            {
                void M()
                {
                    connection?.QueryAsync("select 1");
                    QueryAsync("select 2");
                    QueryAsync<int>("select 3");
                }
            }
            """;

        Assert.Equal(3, Run(source).Count);
    }

    [Fact]
    public void Ignores_dapper_calls_with_no_arguments()
    {
        var source = """
            class C
            {
                void M()
                {
                    connection.QueryAsync();
                }
            }
            """;

        Assert.Empty(Run(source));
    }
}

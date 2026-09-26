namespace Qlcheck.Tests;

public class InlineSqlCheckTests
{
    private static IReadOnlyList<Finding> Run(string source, string path = "Repo.cs")
    {
        var file = new SourceFile(path, source);
        return new InlineSqlCheck().Analyze(file, file.Tree);
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
        Assert.Equal("inline-sql", finding.Check);
        Assert.Equal("Repo.cs", finding.File);
        Assert.Equal(5, finding.Line);
        Assert.Equal(19, finding.Column);
        Assert.Equal(InlineSqlCheck.LayoutMessage, finding.Message);
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
        Assert.Equal(InlineSqlCheck.UnformattableMessage, finding.Message);
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
        Assert.Equal(InlineSqlCheck.UnformattableMessage, finding.Message);
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
        Assert.Equal(InlineSqlCheck.LayoutMessage, finding.Message);
        Assert.Contains("SELECT", finding.Replacement);
        Assert.Contains("1", finding.Replacement);
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
        Assert.StartsWith(InlineSqlCheck.FormatFailedMessage, finding.Message);
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
        Assert.Equal(InlineSqlCheck.LayoutMessage, finding.Message);
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
        Assert.Equal(InlineSqlCheck.LayoutMessage, finding.Message);
        Assert.Contains("coalesce", finding.Replacement, StringComparison.OrdinalIgnoreCase);
    }
}

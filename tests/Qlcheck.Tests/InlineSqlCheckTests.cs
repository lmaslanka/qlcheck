namespace Qlcheck.Tests;

public class InlineSqlCheckTests
{
    private static IReadOnlyList<Finding> Run(string source, string path = "Repo.cs")
    {
        ICheck check = new InlineSqlCheck();
        return check.Run(new CheckContext([new SourceFile(path, source)]));
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

        var findings = Run(source);

        var finding = Assert.Single(findings);
        Assert.Equal("inline-sql", finding.Check);
        Assert.Equal("Repo.cs", finding.File);
        Assert.Equal(5, finding.Line);
        Assert.Equal(19, finding.Column);
        Assert.Equal(InlineSqlCheck.LayoutMessage, finding.Message);
        Assert.Equal(
            "\"\"\"\n" +
            "              SELECT c.record_id,\n" +
            "                     c.name,\n" +
            "                     c.code_alpha2\n" +
            "                FROM countries AS c\n" +
            "               WHERE c.is_deleted = FALSE\n" +
            "            ORDER BY c.name ASC;\n" +
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
        Assert.Contains("SELECT 1", finding.Replacement);
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
            "              SELECT c.record_id,\n" +
            "                     c.name\n" +
            "                FROM countries AS c\n" +
            "               WHERE c.is_deleted = FALSE\n" +
            "            ORDER BY c.name ASC;\n" +
            "            \"\"\";\n" +
            "        connection.QueryAsync(sql);\n" +
            "    }\n" +
            "}\n";

        Assert.Empty(Run(source));
    }
}

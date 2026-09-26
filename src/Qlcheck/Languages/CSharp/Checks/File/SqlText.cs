namespace Qlcheck.Languages.CSharp.Checks.File;

internal static class SqlText
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "INSERT", "UPDATE", "DELETE", "MERGE", "JOIN", "WHERE", "HAVING",
        "VALUES", "UNION", "RETURNING", "TRUNCATE", "DISTINCT", "INNER", "OUTER",
        "LIMIT", "OFFSET", "NULLS", "NULLIF", "COALESCE", "ISNULL", "VARCHAR",
        "NVARCHAR", "COUNT", "CAST", "CASE", "WHEN", "THEN", "ELSE", "ORDER",
        "GROUP", "FROM", "INTO", "EXISTS", "BETWEEN", "LIKE", "ASC", "DESC",
        "OVER", "PARTITION", "LEFT", "RIGHT", "FULL", "CROSS", "WITH", "SET",
    };

    public static bool LooksLikeSql(string value)
    {
        var start = 0;
        for (var i = 0; i <= value.Length; i++)
        {
            if (i == value.Length || !char.IsLetter(value[i]))
            {
                if (i > start && Keywords.Contains(value[start..i]))
                {
                    return true;
                }

                start = i + 1;
            }
        }

        return false;
    }
}

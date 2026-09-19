namespace Qlcheck;

public static class SqlLayout
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "WHERE", "GROUP", "BY", "HAVING", "ORDER", "LIMIT", "OFFSET",
        "INSERT", "INTO", "VALUES", "UPDATE", "SET", "DELETE", "RETURNING", "WITH",
        "JOIN", "INNER", "LEFT", "RIGHT", "FULL", "OUTER", "CROSS", "ON", "USING",
        "UNION", "ALL", "DISTINCT", "AS", "AND", "OR", "NOT", "NULL", "TRUE", "FALSE",
        "IN", "IS", "LIKE", "BETWEEN", "EXISTS", "CASE", "WHEN", "THEN", "ELSE", "END",
        "ASC", "DESC", "OVER", "PARTITION", "ROWS", "RANGE", "UNBOUNDED", "PRECEDING",
        "FOLLOWING", "CURRENT", "ROW", "MERGE", "MATCHED", "CAST",
        "CURRENT_TIMESTAMP", "CURRENT_DATE", "CURRENT_TIME",
    };

    private static readonly string[] ClauseKeywords =
    [
        "LEFT OUTER JOIN", "RIGHT OUTER JOIN", "FULL OUTER JOIN",
        "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN", "CROSS JOIN",
        "UNION ALL", "GROUP BY", "ORDER BY", "INSERT INTO",
        "SELECT", "FROM", "WHERE", "HAVING", "LIMIT", "OFFSET",
        "INSERT", "VALUES", "UPDATE", "SET", "DELETE", "RETURNING",
        "WITH", "JOIN", "ON", "USING", "UNION",
    ];

    public static string Format(string sql)
    {
        var tokens = RewriteCountStar(SqlTokenizer.Tokenize(sql));
        if (tokens.Count == 0)
        {
            return sql.Trim();
        }

        var statements = SplitStatements(tokens);
        return string.Join("\n", statements.Select(FormatStatement));
    }

    private static List<Token> RewriteCountStar(List<Token> tokens)
    {
        var rewritten = new List<Token>(tokens.Count);
        for (var i = 0; i < tokens.Count; i++)
        {
            if (i + 3 < tokens.Count &&
                tokens[i].Kind == TokenKind.Word &&
                tokens[i].Text.Equals("COUNT", StringComparison.OrdinalIgnoreCase) &&
                tokens[i + 1].Kind == TokenKind.LParen &&
                tokens[i + 2].Kind == TokenKind.Other &&
                tokens[i + 2].Text == "*" &&
                tokens[i + 3].Kind == TokenKind.RParen)
            {
                rewritten.Add(tokens[i]);
                rewritten.Add(tokens[i + 1]);
                rewritten.Add(new Token(TokenKind.Number, "1"));
                rewritten.Add(tokens[i + 3]);
                i += 3;
                continue;
            }

            rewritten.Add(tokens[i]);
        }

        return rewritten;
    }

    private static List<List<Token>> SplitStatements(List<Token> tokens)
    {
        var statements = new List<List<Token>>();
        var current = new List<Token>();
        var depth = 0;
        foreach (var token in tokens)
        {
            if (token.Kind == TokenKind.LParen)
            {
                depth++;
            }
            else if (token.Kind == TokenKind.RParen)
            {
                depth = Math.Max(0, depth - 1);
            }

            if (token.Kind == TokenKind.Semicolon && depth == 0)
            {
                current.Add(token);
                if (current.Exists(t => t.Kind != TokenKind.Comment))
                {
                    statements.Add(current);
                }

                current = [];
                continue;
            }

            current.Add(token);
        }

        if (current.Exists(t => t.Kind != TokenKind.Comment))
        {
            statements.Add(current);
        }

        return statements;
    }

    private static string FormatStatement(List<Token> tokens)
    {
        var clauses = SplitClauses(tokens);
        var width = clauses.Max(c => c.Keyword.Length);
        var lines = new List<string>();
        foreach (var clause in clauses)
        {
            lines.AddRange(FormatClause(clause, width));
        }

        return string.Join("\n", lines);
    }

    private static List<Clause> SplitClauses(List<Token> tokens)
    {
        var clauses = new List<Clause>();
        var depth = 0;
        string? keyword = null;
        var body = new List<Token>();

        void Flush()
        {
            if (keyword is null)
            {
                if (body.Count > 0)
                {
                    clauses.Add(new Clause("", body.ToList()));
                }
            }
            else
            {
                clauses.Add(new Clause(keyword, body.ToList()));
            }

            body.Clear();
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind == TokenKind.LParen)
            {
                depth++;
                body.Add(token);
                continue;
            }

            if (token.Kind == TokenKind.RParen)
            {
                depth = Math.Max(0, depth - 1);
                body.Add(token);
                continue;
            }

            if (depth == 0 && TryMatchClause(tokens, i, out var matched, out var consumed))
            {
                Flush();
                keyword = matched;
                i += consumed - 1;
                continue;
            }

            body.Add(token);
        }

        Flush();
        return clauses.Where(c => c.Keyword.Length > 0 || c.Body.Count > 0).ToList();
    }

    private static bool TryMatchClause(List<Token> tokens, int index, out string keyword, out int consumed)
    {
        foreach (var candidate in ClauseKeywords)
        {
            var parts = candidate.Split(' ');
            if (index + parts.Length > tokens.Count)
            {
                continue;
            }

            var match = true;
            for (var p = 0; p < parts.Length; p++)
            {
                var token = tokens[index + p];
                if (token.Kind != TokenKind.Word ||
                    !token.Text.Equals(parts[p], StringComparison.OrdinalIgnoreCase))
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                keyword = candidate;
                consumed = parts.Length;
                return true;
            }
        }

        keyword = "";
        consumed = 0;
        return false;
    }

    private static IEnumerable<string> FormatClause(Clause clause, int width)
    {
        var pad = new string(' ', width - clause.Keyword.Length);
        var keyword = clause.Keyword.Length == 0 ? "" : pad + clause.Keyword;
        var bodyTokens = clause.Body;
        var trailingSemi = bodyTokens.Count > 0 && bodyTokens[^1].Kind == TokenKind.Semicolon;
        if (trailingSemi)
        {
            bodyTokens = bodyTokens.Take(bodyTokens.Count - 1).ToList();
        }

        if (clause.Keyword.Equals("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            var prefix = new List<Token>();
            var i = 0;
            while (i < bodyTokens.Count && bodyTokens[i].Kind == TokenKind.Word &&
                   (bodyTokens[i].Text.Equals("DISTINCT", StringComparison.OrdinalIgnoreCase) ||
                    bodyTokens[i].Text.Equals("ALL", StringComparison.OrdinalIgnoreCase)))
            {
                prefix.Add(bodyTokens[i]);
                i++;
            }

            var selectBody = bodyTokens.Skip(i).ToList();
            var items = SplitTopLevel(selectBody, TokenKind.Comma)
                .Select(RenderTokens)
                .Where(s => s.Length > 0)
                .ToList();
            var prefixText = prefix.Count == 0 ? "" : RenderTokens(prefix) + " ";
            var hang = new string(' ', width + 1);
            for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                var isLast = itemIndex == items.Count - 1;
                var line = itemIndex == 0
                    ? keyword + " " + prefixText + items[itemIndex]
                    : hang + items[itemIndex];
                if (!isLast)
                {
                    line += ",";
                }
                else if (trailingSemi)
                {
                    line += ";";
                }

                yield return line;
            }

            if (items.Count == 0)
            {
                var empty = keyword + (prefixText.Length == 0 ? "" : " " + prefixText.TrimEnd());
                if (trailingSemi)
                {
                    empty += ";";
                }

                yield return empty;
            }

            yield break;
        }

        if (clause.Keyword.Equals("SET", StringComparison.OrdinalIgnoreCase))
        {
            var items = SplitTopLevel(bodyTokens, TokenKind.Comma)
                .Select(RenderTokens)
                .Where(s => s.Length > 0)
                .ToList();
            var hang = new string(' ', width + 1);
            for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                var isLast = itemIndex == items.Count - 1;
                var line = itemIndex == 0
                    ? keyword + " " + items[itemIndex]
                    : hang + items[itemIndex];
                if (!isLast)
                {
                    line += ",";
                }
                else if (trailingSemi)
                {
                    line += ";";
                }

                yield return line;
            }

            if (items.Count == 0)
            {
                yield return trailingSemi ? keyword + ";" : keyword;
            }

            yield break;
        }

        if (clause.Keyword is "WHERE" or "HAVING" or "ON")
        {
            foreach (var line in FormatBooleanClause(keyword, bodyTokens, width, trailingSemi))
            {
                yield return line;
            }

            yield break;
        }

        if ((clause.Keyword is "INSERT INTO" or "VALUES") &&
            TryFormatParenList(keyword, bodyTokens, trailingSemi, out var parenLines))
        {
            foreach (var line in parenLines)
            {
                yield return line;
            }

            yield break;
        }

        var body = RenderTokens(bodyTokens);
        var lineOut = string.IsNullOrEmpty(keyword)
            ? body
            : string.IsNullOrEmpty(body)
                ? keyword
                : keyword + " " + body;
        if (trailingSemi)
        {
            lineOut += ";";
        }

        yield return lineOut;
    }

    private static IEnumerable<string> FormatBooleanClause(
        string keyword,
        List<Token> bodyTokens,
        int width,
        bool trailingSemi)
    {
        var parts = SplitBoolean(bodyTokens);
        if (parts.Count <= 1)
        {
            var body = RenderTokens(bodyTokens);
            var line = string.IsNullOrEmpty(body) ? keyword : keyword + " " + body;
            if (trailingSemi)
            {
                line += ";";
            }

            yield return line;
            yield break;
        }

        for (var i = 0; i < parts.Count; i++)
        {
            var (op, tokens) = parts[i];
            var text = RenderTokens(tokens);
            string line;
            if (i == 0)
            {
                line = string.IsNullOrEmpty(text) ? keyword : keyword + " " + text;
            }
            else
            {
                line = new string(' ', width - op.Length) + op + (text.Length == 0 ? "" : " " + text);
            }

            if (trailingSemi && i == parts.Count - 1)
            {
                line += ";";
            }

            yield return line;
        }
    }

    private static List<(string Op, List<Token> Tokens)> SplitBoolean(List<Token> tokens)
    {
        var parts = new List<(string Op, List<Token> Tokens)>();
        var current = new List<Token>();
        var op = "";
        var depth = 0;
        foreach (var token in tokens)
        {
            if (token.Kind == TokenKind.LParen)
            {
                depth++;
                current.Add(token);
                continue;
            }

            if (token.Kind == TokenKind.RParen)
            {
                depth = Math.Max(0, depth - 1);
                current.Add(token);
                continue;
            }

            if (depth == 0 && token.Kind == TokenKind.Word &&
                (token.Text.Equals("AND", StringComparison.OrdinalIgnoreCase) ||
                 token.Text.Equals("OR", StringComparison.OrdinalIgnoreCase)))
            {
                parts.Add((op, current));
                op = token.Text.ToUpperInvariant();
                current = [];
                continue;
            }

            current.Add(token);
        }

        parts.Add((op, current));
        return parts;
    }

    private static bool TryFormatParenList(
        string keyword,
        List<Token> bodyTokens,
        bool trailingSemi,
        out List<string> lines)
    {
        lines = [];
        if (!TrySplitParenList(bodyTokens, out var prefix, out var items, out var suffix) || items.Count < 2)
        {
            return false;
        }

        var prefixText = RenderTokens(prefix);
        lines.Add(string.IsNullOrEmpty(prefixText) ? keyword + " (" : keyword + " " + prefixText + " (");
        var leading = keyword.TakeWhile(char.IsWhiteSpace).Count();
        var itemIndent = new string(' ', leading + 4);
        var closeIndent = new string(' ', leading);
        for (var i = 0; i < items.Count; i++)
        {
            var line = itemIndent + items[i];
            if (i < items.Count - 1)
            {
                line += ",";
            }

            lines.Add(line);
        }

        var close = closeIndent + ")";
        var suffixText = RenderTokens(suffix);
        if (suffixText.Length > 0)
        {
            close += " " + suffixText;
        }

        if (trailingSemi)
        {
            close += ";";
        }

        lines.Add(close);
        return true;
    }

    private static bool TrySplitParenList(
        List<Token> tokens,
        out List<Token> prefix,
        out List<string> items,
        out List<Token> suffix)
    {
        prefix = [];
        items = [];
        suffix = [];
        var i = 0;
        while (i < tokens.Count && tokens[i].Kind != TokenKind.LParen)
        {
            prefix.Add(tokens[i]);
            i++;
        }

        if (i >= tokens.Count)
        {
            return false;
        }

        i++;
        var inner = new List<Token>();
        var depth = 0;
        for (; i < tokens.Count; i++)
        {
            if (tokens[i].Kind == TokenKind.LParen)
            {
                depth++;
            }
            else if (tokens[i].Kind == TokenKind.RParen)
            {
                if (depth == 0)
                {
                    i++;
                    break;
                }

                depth--;
            }

            inner.Add(tokens[i]);
        }

        items = SplitTopLevel(inner, TokenKind.Comma)
            .Select(RenderTokens)
            .Where(s => s.Length > 0)
            .ToList();
        suffix = tokens.Skip(i).ToList();
        return items.Count >= 2;
    }

    private static List<List<Token>> SplitTopLevel(List<Token> tokens, TokenKind separator)
    {
        var items = new List<List<Token>>();
        var current = new List<Token>();
        var depth = 0;
        foreach (var token in tokens)
        {
            if (token.Kind == TokenKind.LParen)
            {
                depth++;
            }
            else if (token.Kind == TokenKind.RParen)
            {
                depth = Math.Max(0, depth - 1);
            }

            if (token.Kind == separator && depth == 0)
            {
                items.Add(current);
                current = [];
                continue;
            }

            current.Add(token);
        }

        items.Add(current);
        return items;
    }

    private static string RenderTokens(List<Token> tokens)
    {
        var sb = new System.Text.StringBuilder();
        Token? prev = null;
        foreach (var token in tokens)
        {
            if (token.Kind == TokenKind.Comment)
            {
                if (sb.Length > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(token.Text);
                prev = token;
                continue;
            }

            var text = token.Kind == TokenKind.Word && Keywords.Contains(token.Text)
                ? token.Text.ToUpperInvariant()
                : token.Text;

            if (prev is not null && NeedsSpace(prev, token))
            {
                sb.Append(' ');
            }

            sb.Append(text);
            prev = token;
        }

        return sb.ToString();
    }

    private static bool NeedsSpace(Token prev, Token next)
    {
        if (prev.Kind == TokenKind.Word &&
            prev.Text.Equals("COUNT", StringComparison.OrdinalIgnoreCase) &&
            next.Kind == TokenKind.LParen)
        {
            return false;
        }

        if (prev.Kind is TokenKind.LParen or TokenKind.Dot)
        {
            return false;
        }

        if (next.Kind is TokenKind.RParen or TokenKind.Comma or TokenKind.Semicolon or TokenKind.Dot)
        {
            return false;
        }

        return true;
    }

    private sealed record Clause(string Keyword, List<Token> Body);
}

internal enum TokenKind
{
    Word,
    Number,
    String,
    Comma,
    LParen,
    RParen,
    Semicolon,
    Dot,
    Comment,
    Other,
}

internal sealed record Token(TokenKind Kind, string Text);

internal static class SqlTokenizer
{
    public static List<Token> Tokenize(string sql)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < sql.Length)
        {
            var c = sql[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                var start = i;
                i += 2;
                while (i < sql.Length && sql[i] is not '\n' and not '\r')
                {
                    i++;
                }

                tokens.Add(new Token(TokenKind.Comment, sql[start..i].TrimEnd()));
                continue;
            }

            if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                var start = i;
                i += 2;
                while (i + 1 < sql.Length && !(sql[i] == '*' && sql[i + 1] == '/'))
                {
                    i++;
                }

                if (i + 1 < sql.Length)
                {
                    i += 2;
                }

                tokens.Add(new Token(TokenKind.Comment, sql[start..Math.Min(i, sql.Length)]));
                continue;
            }

            if (c == '\'')
            {
                tokens.Add(ReadQuoted(sql, ref i, '\'', '\'', escapeByDoubling: true));
                continue;
            }

            if (c == '"')
            {
                tokens.Add(ReadQuoted(sql, ref i, '"', '"', escapeByDoubling: true));
                continue;
            }

            if (c == '[')
            {
                tokens.Add(ReadQuoted(sql, ref i, '[', ']', escapeByDoubling: false));
                continue;
            }

            if (c == '`')
            {
                tokens.Add(ReadQuoted(sql, ref i, '`', '`', escapeByDoubling: true));
                continue;
            }

            if (c == '$')
            {
                if (TryReadDollarQuote(sql, ref i, out var dollar))
                {
                    tokens.Add(dollar);
                    continue;
                }
            }

            switch (c)
            {
                case ',':
                    tokens.Add(new Token(TokenKind.Comma, ","));
                    i++;
                    continue;
                case '(':
                    tokens.Add(new Token(TokenKind.LParen, "("));
                    i++;
                    continue;
                case ')':
                    tokens.Add(new Token(TokenKind.RParen, ")"));
                    i++;
                    continue;
                case ';':
                    tokens.Add(new Token(TokenKind.Semicolon, ";"));
                    i++;
                    continue;
                case '.':
                    tokens.Add(new Token(TokenKind.Dot, "."));
                    i++;
                    continue;
            }

            if (char.IsDigit(c))
            {
                var start = i;
                i++;
                while (i < sql.Length && (char.IsDigit(sql[i]) || sql[i] == '.'))
                {
                    i++;
                }

                tokens.Add(new Token(TokenKind.Number, sql[start..i]));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                i++;
                while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_'))
                {
                    i++;
                }

                tokens.Add(new Token(TokenKind.Word, sql[start..i]));
                continue;
            }

            if ((c is '@' or ':') && i + 1 < sql.Length && (char.IsLetter(sql[i + 1]) || sql[i + 1] == '_'))
            {
                var start = i;
                i++;
                while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_'))
                {
                    i++;
                }

                tokens.Add(new Token(TokenKind.Word, sql[start..i]));
                continue;
            }

            var otherStart = i;
            i++;
            while (i < sql.Length && !char.IsWhiteSpace(sql[i]) && !char.IsLetterOrDigit(sql[i]) &&
                   sql[i] is not '(' and not ')' and not ',' and not ';' and not '.' and not '\'' and not '"'
                       and not '[' and not '`' and not '$' and not '-' and not '/')
            {
                i++;
            }

            tokens.Add(new Token(TokenKind.Other, sql[otherStart..i]));
        }

        return tokens;
    }

    private static Token ReadQuoted(string sql, ref int i, char open, char close, bool escapeByDoubling)
    {
        var start = i;
        i++;
        while (i < sql.Length)
        {
            if (escapeByDoubling && sql[i] == close && i + 1 < sql.Length && sql[i + 1] == close)
            {
                i += 2;
                continue;
            }

            if (sql[i] == close)
            {
                i++;
                break;
            }

            i++;
        }

        return new Token(TokenKind.String, sql[start..i]);
    }

    private static bool TryReadDollarQuote(string sql, ref int i, out Token token)
    {
        var start = i;
        var j = i + 1;
        while (j < sql.Length && (char.IsLetterOrDigit(sql[j]) || sql[j] == '_'))
        {
            j++;
        }

        if (j >= sql.Length || sql[j] != '$')
        {
            token = null!;
            return false;
        }

        var tag = sql[start..(j + 1)];
        j++;
        var end = sql.IndexOf(tag, j, StringComparison.Ordinal);
        if (end < 0)
        {
            token = null!;
            return false;
        }

        i = end + tag.Length;
        token = new Token(TokenKind.String, sql[start..i]);
        return true;
    }
}

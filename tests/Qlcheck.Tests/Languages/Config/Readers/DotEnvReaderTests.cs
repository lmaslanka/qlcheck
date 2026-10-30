using Qlcheck.Languages.Config.Readers;

namespace Qlcheck.Tests;

public class DotEnvReaderTests
{
    [Fact]
    public void Read_returns_keys_values_and_value_positions()
    {
        const string text = "# settings\n\nAPI_URL=https://api\nexport SMTP_USER = mailer\n";

        var entries = DotEnvReader.Read(text);

        Assert.Equal([ConfigText.Entry(text, "API_URL", "https://api"), ConfigText.Entry(text, "SMTP_USER", "mailer")], entries);
    }

    [Fact]
    public void Read_strips_matching_quotes_and_unquoted_trailing_comments()
    {
        const string text = "A=\"quoted # kept\"\nB='single'\nC=plain # comment\nD=a#b\n";

        var entries = DotEnvReader.Read(text);

        Assert.Equal(["quoted # kept", "single", "plain", "a#b"], entries.Select(entry => entry.Value));
        Assert.Equal(ConfigText.PositionOf(text, "quoted"), (entries[0].Line, entries[0].Column));
    }

    [Fact]
    public void Read_handles_a_bom_and_crlf()
    {
        const string text = "A=first\r\nB=second\r\n";

        var entries = DotEnvReader.Read($"﻿{text}");

        Assert.Equal([ConfigText.Entry(text, "A", "first"), ConfigText.Entry(text, "B", "second")], entries);
    }

    [Fact]
    public void Read_marks_connection_string_variables()
    {
        var entry = Assert.Single(DotEnvReader.Read("ConnectionStrings__Default=Server=db\n"));

        Assert.True(entry.IsConnectionString);
        Assert.Equal("Server=db", entry.Value);
    }

    [Fact]
    public void Read_skips_lines_that_are_not_entries()
    {
        Assert.Empty(DotEnvReader.Read("just text\n=novalue\n   \n#A=1\n"));
    }
}

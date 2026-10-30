using Qlcheck.Languages.Config.Readers;

namespace Qlcheck.Tests;

public class JsonSettingsReaderTests
{
    [Fact]
    public void Read_joins_sections_with_colons_and_positions_each_value()
    {
        const string text = """
            {
              "Logging": { "Level": "Warning" },
              "Smtp": {
                "Host": "mail.local",
                "Port": 25
              }
            }
            """;

        var entries = JsonSettingsReader.Read(text);

        Assert.Equal([ConfigText.Entry(text, "Logging:Level", "Warning"), ConfigText.Entry(text, "Smtp:Host", "mail.local")], entries);
    }

    [Fact]
    public void Read_numbers_array_elements_like_dotnet_configuration()
    {
        const string text = """{ "Hosts": [ "one", { "Name": "two" }, 3, "four" ] }""";

        var entries = JsonSettingsReader.Read(text);

        Assert.Equal(["Hosts:0", "Hosts:1:Name", "Hosts:3"], entries.Select(entry => entry.KeyPath));
    }

    [Fact]
    public void Read_allows_comments_and_trailing_commas()
    {
        const string text = """
            {
              // a comment
              "A": "first", /* another */
              "B": "second",
            }
            """;

        Assert.Equal(["first", "second"], JsonSettingsReader.Read(text).Select(entry => entry.Value));
    }

    [Fact]
    public void Read_handles_a_bom_crlf_and_non_ascii_text_before_a_value()
    {
        const string text = "{\r\n  \"Name\": \"Zoë\", \"Next\": \"after\"\r\n}";

        var entries = JsonSettingsReader.Read($"﻿{text}");

        Assert.Equal([ConfigText.Entry(text, "Name", "Zoë"), ConfigText.Entry(text, "Next", "after")], entries);
    }

    [Fact]
    public void Read_marks_connection_strings()
    {
        const string text = """{ "ConnectionStrings": { "Default": "Server=db" }, "Other": "x" }""";

        var entries = JsonSettingsReader.Read(text);

        Assert.True(entries[0].IsConnectionString);
        Assert.False(entries[1].IsConnectionString);
    }

    [Theory]
    [InlineData("{ \"A\": ")]
    [InlineData("not json")]
    [InlineData("{ \"A\": \"x\" } trailing")]
    public void Read_yields_nothing_for_a_malformed_file(string text)
    {
        Assert.Empty(JsonSettingsReader.Read(text));
    }
}

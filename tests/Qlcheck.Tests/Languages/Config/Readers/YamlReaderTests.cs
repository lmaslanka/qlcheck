using Qlcheck.Languages.Config.Readers;

namespace Qlcheck.Tests;

public class YamlReaderTests
{
    [Fact]
    public void Read_joins_nested_keys_and_positions_each_value()
    {
        const string text = """
            # settings
            Smtp:
              Host: mail.local
              Auth:
                User: mailer
            Logging: warn
            """;

        var entries = YamlReader.Read(text);

        Assert.Equal(
            [
                ConfigText.Entry(text, "Smtp:Host", "mail.local"),
                ConfigText.Entry(text, "Smtp:Auth:User", "mailer"),
                ConfigText.Entry(text, "Logging", "warn"),
            ],
            entries);
    }

    [Fact]
    public void Read_strips_quotes_and_trailing_comments()
    {
        const string text = """
            a: "double # kept"
            b: 'single'
            c: plain # comment
            "d": url://x:1
            """;

        var entries = YamlReader.Read(text);

        Assert.Equal(
            [("a", "double # kept"), ("b", "single"), ("c", "plain"), ("d", "url://x:1")],
            entries.Select(entry => (entry.KeyPath, entry.Value)));
        Assert.Equal(ConfigText.PositionOf(text, "double"), (entries[0].Line, entries[0].Column));
    }

    [Fact]
    public void Read_reads_docker_compose_environment_lists_and_list_mappings()
    {
        const string text = """
            services:
              web:
                environment:
                  - SMTP_USER=mailer
                  - "QUOTED=value"
                ports:
                  - name: http
            """;

        var entries = YamlReader.Read(text);

        Assert.Equal(
            [
                ConfigText.Entry(text, "services:web:environment:SMTP_USER", "mailer"),
                ConfigText.Entry(text, "services:web:environment:QUOTED", "value"),
                ConfigText.Entry(text, "services:web:ports:name", "http"),
            ],
            entries);
    }

    [Fact]
    public void Read_joins_block_scalars_and_positions_them_at_the_first_line()
    {
        const string text = """
            literal: |
              first
              second
            folded: >
              one
              two
            """;

        var entries = YamlReader.Read(text);

        Assert.Equal([ConfigText.Entry(text, "literal", "first\nsecond", locate: "first"), ConfigText.Entry(text, "folded", "one two", locate: "one")], entries);
    }

    [Fact]
    public void Read_starts_over_at_a_document_break_and_skips_what_it_does_not_parse()
    {
        const string text = """
            outer:
              inner: one
            ---
            top: two
            anchor: &base value
            alias: *base
            flow: { a: b }
            list: [a, b]
            tagged: !secret value
            """;

        Assert.Equal(["outer:inner", "top"], YamlReader.Read(text).Select(entry => entry.KeyPath));
    }

    [Fact]
    public void Read_handles_a_bom_and_crlf()
    {
        const string text = "a: first\r\nb:\r\n  c: second\r\n";

        var entries = YamlReader.Read($"﻿{text}");

        Assert.Equal([ConfigText.Entry(text, "a", "first"), ConfigText.Entry(text, "b:c", "second")], entries);
    }

    [Fact]
    public void Read_marks_connection_strings()
    {
        const string text = "ConnectionStrings:\n  Default: Server=db\n";

        Assert.True(Assert.Single(YamlReader.Read(text)).IsConnectionString);
    }

    [Fact]
    public void Read_yields_nothing_for_text_that_is_not_yaml_and_never_throws()
    {
        Assert.Empty(YamlReader.Read("just some words\n\t\n:::\n- \n"));
    }
}

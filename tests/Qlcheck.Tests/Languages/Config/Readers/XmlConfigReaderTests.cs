using Qlcheck.Languages.Config.Readers;

namespace Qlcheck.Tests;

public class XmlConfigReaderTests
{
    private const string ValueAttribute = "value=";

    [Fact]
    public void Read_turns_add_key_value_into_the_key_positioned_at_the_value_attribute()
    {
        const string text = """
            <configuration>
              <appSettings>
                <add key="Smtp:Host" value="mail.local" />
              </appSettings>
            </configuration>
            """;

        var entry = Assert.Single(XmlConfigReader.Read(text));

        Assert.Equal(ConfigText.Entry(text, "Smtp:Host", "mail.local", locate: ValueAttribute), entry);
    }

    [Fact]
    public void Read_names_connection_strings_by_their_name()
    {
        const string text = """
            <configuration>
              <connectionStrings>
                <add name="Default" connectionString="Server=sql" providerName="x" />
              </connectionStrings>
            </configuration>
            """;

        var entry = Assert.Single(XmlConfigReader.Read(text));

        Assert.Equal("ConnectionStrings:Default", entry.KeyPath);
        Assert.Equal("Server=sql", entry.Value);
        Assert.True(entry.IsConnectionString);
    }

    [Fact]
    public void Read_turns_other_attributes_and_element_text_into_entries()
    {
        const string text = """
            <configuration xmlns="urn:x">
              <network host="mail.local" userName="mailer" />
              <Password>hunter</Password>
            </configuration>
            """;

        var entries = XmlConfigReader.Read(text);

        Assert.Equal(
            [("network:host", "mail.local"), ("network:userName", "mailer"), ("Password", "hunter")],
            entries.Select(entry => (entry.KeyPath, entry.Value)));
        Assert.Equal(ConfigText.PositionOf(text, "hunter"), (entries[^1].Line, entries[^1].Column));
    }

    [Fact]
    public void Read_reads_nuget_package_source_credentials()
    {
        const string text = """
            <configuration>
              <packageSourceCredentials>
                <Feed>
                  <add key="Username" value="me" />
                  <add key="ClearTextPassword" value="hunter" />
                </Feed>
              </packageSourceCredentials>
            </configuration>
            """;

        Assert.Equal(["Username", "ClearTextPassword"], XmlConfigReader.Read(text).Select(entry => entry.KeyPath));
    }

    [Fact]
    public void Read_handles_a_bom_and_crlf()
    {
        const string text = "<c>\r\n  <add key=\"A\" value=\"first\" />\r\n</c>";

        var entry = Assert.Single(XmlConfigReader.Read($"﻿{text}"));

        Assert.Equal(ConfigText.Entry(text, "A", "first", locate: ValueAttribute), entry);
    }

    [Theory]
    [InlineData("<!DOCTYPE c [<!ENTITY e \"v\">]><c><add key=\"A\" value=\"&e;\" /></c>")]
    [InlineData("<c><add key=\"A\" value=\"x\"></c>")]
    [InlineData("not xml")]
    public void Read_refuses_dtds_and_yields_nothing_for_a_malformed_document(string text)
    {
        Assert.Empty(XmlConfigReader.Read(text));
    }
}

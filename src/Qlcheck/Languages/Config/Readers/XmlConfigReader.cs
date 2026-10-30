// Copyright (c) qlcheck contributors.
using System.Xml;

namespace Qlcheck.Languages.Config.Readers;

// `*.config` files (web.config, app.config, NuGet.config). `<add key="K" value="V"/>` is the entry
// K, `<connectionStrings><add name="N" connectionString="V"/>` is `ConnectionStrings:N`, any other
// attribute is `element:attribute`, and element text is `element`. Attribute entries are positioned
// at the attribute. DTDs are refused and nothing external is resolved; a document that does not
// parse yields no entries.
internal static class XmlConfigReader
{
    private const string AddTag = "add";

    private const string ConnectionStringsTag = "connectionStrings";

    private const string KeyAttribute = "key";

    private const string ValueAttribute = "value";

    private const string NameAttribute = "name";

    private const string ConnectionStringAttribute = "connectionString";

    private const string ConnectionStringsSection = "ConnectionStrings:";

    private const string Separator = ":";

    private const string NamespaceAttribute = "xmlns";

    private static readonly XmlReaderSettings Settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreWhitespace = true,
    };

    public static IReadOnlyList<ConfigEntry> Read(string text)
    {
        var entries = new List<ConfigEntry>();
        var elements = new Stack<string>();
        try
        {
            using var reader = XmlReader.Create(new StringReader(text.TrimStart('﻿')), Settings);
            var info = (IXmlLineInfo)reader;
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        ReadElement(reader, info, elements, entries);
                        break;
                    case XmlNodeType.EndElement:
                        elements.Pop();
                        break;
                    case XmlNodeType.Text or XmlNodeType.CDATA when elements.Count > 0:
                        entries.Add(ConfigEntry.At(elements.Peek(), reader.Value.Trim(), info.LineNumber, info.LinePosition));
                        break;
                    default:
                        break;
                }
            }
        }
        catch (XmlException)
        {
            return [];
        }

        return entries;
    }

    private static void ReadElement(XmlReader reader, IXmlLineInfo info, Stack<string> elements, List<ConfigEntry> entries)
    {
        var name = reader.LocalName;
        var parent = elements.Count > 0 ? elements.Peek() : null;
        var attributes = Attributes(reader, info);
        entries.AddRange(Entries(name, parent, attributes));
        if (!reader.IsEmptyElement)
        {
            elements.Push(name);
        }
    }

    private static Dictionary<string, (string Value, int Line, int Column)> Attributes(XmlReader reader, IXmlLineInfo info)
    {
        var attributes = new Dictionary<string, (string Value, int Line, int Column)>(StringComparer.Ordinal);
        while (reader.MoveToNextAttribute())
        {
            if (reader.Prefix != NamespaceAttribute && reader.LocalName != NamespaceAttribute)
            {
                attributes[reader.LocalName] = (reader.Value, info.LineNumber, info.LinePosition);
            }
        }

        reader.MoveToElement();
        return attributes;
    }

    private static IEnumerable<ConfigEntry> Entries(
        string element,
        string? parent,
        Dictionary<string, (string Value, int Line, int Column)> attributes)
    {
        if (element == AddTag
            && parent == ConnectionStringsTag
            && attributes.TryGetValue(NameAttribute, out var name)
            && attributes.TryGetValue(ConnectionStringAttribute, out var connection))
        {
            return [ConfigEntry.At(ConnectionStringsSection + name.Value, connection.Value, connection.Line, connection.Column)];
        }

        if (element == AddTag
            && attributes.TryGetValue(KeyAttribute, out var key)
            && attributes.TryGetValue(ValueAttribute, out var value))
        {
            return [ConfigEntry.At(key.Value, value.Value, value.Line, value.Column)];
        }

        return attributes.Select(pair =>
            ConfigEntry.At(element + Separator + pair.Key, pair.Value.Value, pair.Value.Line, pair.Value.Column));
    }
}

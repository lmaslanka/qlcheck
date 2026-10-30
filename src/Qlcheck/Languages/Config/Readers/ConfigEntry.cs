// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.Config.Readers;

// One string value from a configuration file: its key path (`:`-joined sections, as .NET
// configuration names them), the value, and the 1-based position where the value starts.
internal sealed record ConfigEntry(string KeyPath, string Value, int Line, int Column, bool IsConnectionString)
{
    private const string ConnectionStringsSection = "ConnectionStrings:";

    private const string ConnectionStringsVariable = "ConnectionStrings__";

    public static ConfigEntry At(string keyPath, string value, int line, int column) =>
        new(keyPath, value, line, column, IsConnectionStringKey(keyPath));

    private static bool IsConnectionStringKey(string keyPath) =>
        keyPath.StartsWith(ConnectionStringsSection, StringComparison.OrdinalIgnoreCase)
        || keyPath.StartsWith(ConnectionStringsVariable, StringComparison.OrdinalIgnoreCase);
}

// Copyright (c) qlcheck contributors.
using System.Text.Json;

namespace Qlcheck.Languages.Config;

// The config Language's Checks, from its embedded checks.json: the same id-and-message rows as the
// C# catalog.
internal static class ConfigCatalog
{
    private const string ResourceName = "Qlcheck.Languages.Config.checks.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyList<ConfigCheck> Checks { get; } = Load();

    private static List<ConfigCheck> Load()
    {
        using var stream = typeof(ConfigCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("Config catalog resource is missing.");
        var rows = JsonSerializer.Deserialize<List<RowDto>>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Config catalog resource is empty.");
        return rows.Select(row => new ConfigCheck(row.Id, row.Message)).ToList();
    }

    private sealed record RowDto(string Id, string Message);
}

// Copyright (c) qlcheck contributors.
using System.Text.Json;

namespace Qlcheck.Languages.CSharp.Catalog;

internal static class Catalog
{
    private const string ResourceName = "Qlcheck.Languages.CSharp.Catalog.checks.json";

    private const string MissingResource = "Catalog resource is missing.";

    private const string EmptyResource = "Catalog resource is empty.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyList<CatalogCheck> Checks { get; } = Load();

    public static IReadOnlyDictionary<string, string> Messages { get; } =
        Checks.ToDictionary(check => check.Id, check => check.Message, StringComparer.Ordinal);

    private static IReadOnlyList<CatalogCheck> Load()
    {
        using var stream = Open();
        var rows = JsonSerializer.Deserialize<List<RowDto>>(stream, JsonOptions);
        if (rows is null)
        {
            throw new InvalidOperationException(EmptyResource);
        }

        return rows.Select(row => new CatalogCheck(row.Id, row.Message)).ToList();
    }

    private static Stream Open()
    {
        var stream = typeof(Catalog).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            throw new InvalidOperationException(MissingResource);
        }

        return stream;
    }

    private sealed record RowDto(string Id, string Message);
}

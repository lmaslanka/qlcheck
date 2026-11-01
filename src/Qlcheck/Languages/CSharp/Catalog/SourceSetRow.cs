// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog;

internal sealed class SourceSetRow
{
    public required string Id { get; init; }

    public required CheckClass Class { get; init; }

    public required Action<SourceSetContext> Apply { get; init; }
}

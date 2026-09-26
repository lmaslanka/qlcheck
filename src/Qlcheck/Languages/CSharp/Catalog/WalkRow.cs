// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog;

internal sealed class WalkRow
{
    public required string Id { get; init; }

    public required CheckClass Class { get; init; }

    public required Action<WalkContext> Apply { get; init; }
}

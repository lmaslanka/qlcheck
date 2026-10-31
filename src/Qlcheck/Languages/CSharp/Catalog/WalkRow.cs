// Copyright (c) qlcheck contributors.
using Qlcheck.Languages.CSharp.Catalog.Dataflow;

namespace Qlcheck.Languages.CSharp.Catalog;

internal sealed class WalkRow
{
    public required string Id { get; init; }

    public required CheckClass Class { get; init; }

    public required Action<WalkContext> Apply { get; init; }

    // The sinks and labels of a dataflow Taint row (Row.Flow); null for every other row.
    public TaintSpec? Taint { get; init; }
}

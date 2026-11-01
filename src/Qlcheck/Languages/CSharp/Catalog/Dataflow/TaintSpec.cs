// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// A Taint row as data: the sinks it watches, the labels that make a value harmful there, and the
// fix hint added to each Finding's message. PublicParameters says whether a public method's
// parameter counts as untrusted for this row (D1): yes where a public API is an entry point (SQL,
// commands), no where wrappers that take the value on purpose are the norm (file paths).
internal sealed record TaintSpec(TaintLabel Accepts, IReadOnlyList<Sink> Sinks, string Hint)
{
    public bool PublicParameters { get; init; }
}

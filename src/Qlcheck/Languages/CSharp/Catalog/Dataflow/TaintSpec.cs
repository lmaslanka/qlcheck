// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// A Taint row as data: the sinks it watches, the labels that make a value harmful there, and the
// fix hint added to each Finding's message.
internal sealed record TaintSpec(TaintLabel Accepts, IReadOnlyList<Sink> Sinks, string Hint);

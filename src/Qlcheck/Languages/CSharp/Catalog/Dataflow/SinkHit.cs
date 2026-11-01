// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;

namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// Tainted data reaching a sink: the call, creation or assignment where it lands, the member it
// lands in as the message shows it ("`DbCommand.CommandText`", "an array size"), the
// labels it carries there, its source and the locals it went through.
internal sealed record SinkHit(Sink Sink, SyntaxNode Node, string Target, TaintLabel Labels, TaintSource Source, IReadOnlyList<string> Via);

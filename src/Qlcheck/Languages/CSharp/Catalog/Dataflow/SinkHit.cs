// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;

namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// Tainted data reaching a sink: the call, creation or assignment where it lands, the labels it
// carries there, its source and the locals it went through.
internal sealed record SinkHit(Sink Sink, SyntaxNode Node, TaintLabel Labels, TaintSource Source, IReadOnlyList<string> Via);

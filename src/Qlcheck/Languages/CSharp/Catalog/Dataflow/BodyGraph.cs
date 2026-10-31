// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;

namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// One executable body of a file and its control-flow graph. Owner is the method whose parameters
// are the body's inputs: the method, constructor, operator or accessor (the getter for an
// expression-bodied property), or the compiler's synthesized entry point for top-level statements.
// Null only when no symbol binds. Lambdas and local functions are not bodies of their own; they are
// sub-graphs of the graph that contains them.
internal sealed record BodyGraph(SyntaxNode Declaration, IMethodSymbol? Owner, ControlFlowGraph Graph);

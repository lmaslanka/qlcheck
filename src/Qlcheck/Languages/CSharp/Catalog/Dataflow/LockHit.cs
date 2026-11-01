// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;

namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// A lock problem at an acquisition or release, with the lock as written (`_gate`).
internal sealed record LockHit(LockProblem Problem, SyntaxNode Node, string Lock);

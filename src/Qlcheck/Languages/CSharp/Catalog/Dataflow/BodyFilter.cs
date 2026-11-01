// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;

namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// Whether a body is worth binding, judged from its declaration and the tokens of its executable part.
internal delegate bool BodyFilter(SyntaxNode declaration, IReadOnlyList<SyntaxToken> tokens);

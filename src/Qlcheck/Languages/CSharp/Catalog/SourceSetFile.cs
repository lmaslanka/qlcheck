// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;

namespace Qlcheck.Languages.CSharp.Catalog;

// One scanned file in a run-wide Check: its Finding path and its syntax tree.
internal sealed record SourceSetFile(string Path, SyntaxTree Tree);

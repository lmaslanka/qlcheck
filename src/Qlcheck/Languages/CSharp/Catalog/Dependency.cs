// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;

namespace Qlcheck.Languages.CSharp.Catalog;

// Code under Source (its innermost type, or namespace) referencing Target (a type, or for a using
// directive a namespace), both as dotted names, at Location in the file at Path.
internal sealed record Dependency(string Source, string Target, string Path, Location Location);

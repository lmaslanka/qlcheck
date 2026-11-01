// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;

namespace Qlcheck.Languages.CSharp.Catalog;

// Code under Source (its innermost type, or namespace) referencing Target (a type, or for a using
// directive a namespace), both as dotted names, at Location in the file at Path. The namespaces
// are where each side lives (the side itself when it is a namespace), for grouping into components.
internal sealed record Dependency(string Source, string Target, string Path, Location Location, string SourceNamespace, string TargetNamespace);

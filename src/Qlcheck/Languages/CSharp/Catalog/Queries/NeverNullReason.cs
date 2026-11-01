// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;

namespace Qlcheck.Languages.CSharp.Catalog.Queries;

// A Never-null result: the callee, why its result cannot be null, and the type it returns (the
// awaited type for an awaited call).
internal sealed record NeverNullReason(NeverNullKind Kind, IMethodSymbol Method, ITypeSymbol Type);

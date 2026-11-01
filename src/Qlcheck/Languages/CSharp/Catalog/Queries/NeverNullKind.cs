// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog.Queries;

// Why a call's result cannot be null.
internal enum NeverNullKind
{
    // The callee is annotated to return a non-nullable reference.
    Declared,

    // The callee has no annotations, cannot be overridden, and returns only new objects, empty
    // collections, literals and other never-null results.
    ReturnsNewOrEmpty,

    // The callee has no annotations and is an iterator, which never returns null.
    Iterator,
}

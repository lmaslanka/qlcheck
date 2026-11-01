// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

internal enum SinkKind
{
    Call,
    Set,
    Ctor,

    // Structural sinks, matched by shape rather than by member: the size of a new array, and an
    // operand of a loop's relational condition.
    ArraySize,
    LoopBound,
}

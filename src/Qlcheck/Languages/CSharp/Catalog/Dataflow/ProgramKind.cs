// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// What a process sink requires of the program being started.
internal enum ProgramKind
{
    Any,
    Shell,
    NotShell,
}

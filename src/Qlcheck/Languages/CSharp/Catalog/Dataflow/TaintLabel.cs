// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// What a tainted value is. A sanitizer changes the label rather than deleting it, so one analysis
// serves every row: `int.Parse` turns Untrusted into Numeric, which only the allocation and loop
// rows accept.
[Flags]
internal enum TaintLabel
{
    None = 0,
    Untrusted = 1,
    Numeric = 2,
    FileName = 4,
    ContainedPath = 8,
    ArchiveEntry = 16,
    ExceptionDetail = 32,

    // A parameter of a method visible outside the assembly: untrusted for rows where a public
    // API is an entry point (SQL, commands), but not for rows where wrappers that take the value
    // on purpose are the norm (file paths). Entry points proper (requests, console) are Untrusted.
    PublicParameter = 64,
}

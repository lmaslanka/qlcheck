// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

internal enum LockProblem
{
    // Still held when the method returns on some path, though released on another (lock-released).
    NotReleasedOnAllPaths,

    // Acquired and released nowhere in the method (release-lock-same-method).
    NeverReleased,

    // A reader-writer lock released in a mode it does not hold (matching-lock-release).
    ModeMismatch,
}

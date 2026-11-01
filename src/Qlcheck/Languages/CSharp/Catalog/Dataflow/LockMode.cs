// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// How a lock is held: Monitor, SpinLock, Mutex and semaphores are exclusive; a reader-writer lock is
// held for reading, writing, or upgradeable reading.
internal enum LockMode
{
    Exclusive,
    Read,
    Write,
    Upgradeable,
}

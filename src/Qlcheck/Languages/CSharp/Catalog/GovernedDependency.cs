// Copyright (c) qlcheck contributors.
using Qlcheck.Scan;

namespace Qlcheck.Languages.CSharp.Catalog;

// A dependency in a file that a .qlcheck_architecture governs.
internal sealed record GovernedDependency(ArchitectureFile File, Dependency Dependency);

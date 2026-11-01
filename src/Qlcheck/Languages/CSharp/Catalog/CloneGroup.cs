// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog;

// Every copy of one duplicated statement sequence, sorted by path and position.
internal sealed record CloneGroup(IReadOnlyList<CloneCopy> Copies, int Tokens);

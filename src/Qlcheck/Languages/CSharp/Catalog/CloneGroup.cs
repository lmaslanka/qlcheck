// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog;

// Every copy of one duplicated statement sequence, sorted by path and position.
internal sealed record CloneGroup(IReadOnlyList<CloneCopy> Copies, int Tokens)
{
    // True when every copy has the same tokens, names included. Otherwise identifiers were renamed, see plan 09.
    public bool Exact => Copies.Select(copy => copy.TokenText).Distinct(StringComparer.Ordinal).Count() == 1;
}

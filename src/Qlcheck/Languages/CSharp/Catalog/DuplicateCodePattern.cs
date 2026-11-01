// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog;

// duplicate-code: one Finding per copy of a duplicated statement sequence (D7), at the copy, naming
// the other copies, so --uncommitted, disabling and suppression all work on each copy alone.
internal static class DuplicateCodePattern
{
    public static void Apply(SourceSetContext ctx, string id)
    {
        foreach (var group in CloneIndex.Find(ctx.Files))
        {
            foreach (var copy in group.Copies)
            {
                ctx.ReportCustom(id, copy.Path, copy.Location, Message(copy, group.Copies));
            }
        }
    }

    // "Lines 82-89 duplicate src/A.cs:103-110, src/A.cs:145-152 and 2 more places; extract one
    // shared method and call it from each copy."
    public static string Message(CloneCopy copy, IReadOnlyList<CloneCopy> copies)
    {
        var others = copies.Where(other => other != copy).Select(other => other.ToString()).ToList();
        var named = others.Take(Thresholds.DuplicateOthersNamed).ToList();
        var rest = others.Count - named.Count;
        return $"Lines {copy.StartLine}-{copy.EndLine} duplicate {List(named, rest)}; extract one shared method and call it from each copy.";
    }

    private static string List(List<string> named, int rest)
    {
        if (rest > 0)
        {
            return $"{string.Join(", ", named)} and {rest} more {(rest == 1 ? "place" : "places")}";
        }

        return named.Count == 1 ? named[0] : $"{string.Join(", ", named[..^1])} and {named[^1]}";
    }
}

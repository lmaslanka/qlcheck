// Copyright (c) qlcheck contributors.
using System.Collections.Immutable;

namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// The taint of one value: its labels, the first source that reached it, and the locals it passed
// through on the way. Joining two taints unions the labels and keeps the first source, so the
// analysis converges on labels alone and the message stays stable.
internal readonly record struct Taint(TaintLabel Labels, TaintSource? Source, ImmutableArray<string> Via)
{
    public static Taint None => default;

    public bool IsNone => Labels == TaintLabel.None;

    public IReadOnlyList<string> Path => Via.IsDefault ? [] : Via;

    public static Taint From(TaintLabel labels, TaintSource source) => new(labels, source, []);

    public Taint Join(Taint other)
    {
        if (IsNone)
        {
            return other;
        }

        return other.IsNone ? this : this with { Labels = Labels | other.Labels };
    }

    public Taint Relabel(TaintLabel labels) => labels == TaintLabel.None ? None : this with { Labels = labels };

    // Recorded when the value is stored in a local, so the message can say `via query`.
    public Taint Through(string local)
    {
        if (IsNone || Path.Contains(local))
        {
            return this;
        }

        return this with { Via = Via.IsDefault ? [local] : Via.Add(local) };
    }
}

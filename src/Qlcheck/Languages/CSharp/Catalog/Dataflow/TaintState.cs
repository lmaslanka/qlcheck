// Copyright (c) qlcheck contributors.
namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// The taint of each tracked storage location at one program point. Keys are locals, parameters and
// the compiler's flow-capture ids. An absent key is untainted.
internal sealed class TaintState
{
    private readonly Dictionary<object, Taint> _taints;

    public TaintState()
        : this([])
    {
    }

    private TaintState(Dictionary<object, Taint> taints)
    {
        _taints = taints;
    }

    public Taint Get(object key) => _taints.TryGetValue(key, out var taint) ? taint : Taint.None;

    public void Set(object key, Taint taint)
    {
        if (taint.IsNone)
        {
            _taints.Remove(key);
        }
        else
        {
            _taints[key] = taint;
        }
    }

    public TaintState Clone() => new(new Dictionary<object, Taint>(_taints));

    // Unions this state into `into` and reports whether any label there grew.
    public bool MergeInto(TaintState into)
    {
        var changed = false;
        foreach (var (key, taint) in _taints)
        {
            var old = into.Get(key);
            var merged = old.Join(taint);
            if (merged.Labels != old.Labels)
            {
                into._taints[key] = merged;
                changed = true;
            }
        }

        return changed;
    }
}

// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// The taint of each tracked storage location at one program point. Keys are locals, parameters,
// fields and the compiler's flow-capture ids. An absent key is untainted.
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

    // The key a reference to a local, parameter, flow capture, or a field of `this` or a static
    // field, is tracked under; null otherwise. A field is only tracked within the member that
    // writes it: every body starts with all fields clean.
    public static object? KeyOf(IOperation operation) =>
        operation switch
        {
            ILocalReferenceOperation local => local.Local,
            IParameterReferenceOperation parameter => parameter.Parameter,
            IFlowCaptureReferenceOperation capture => capture.Id,
            IFieldReferenceOperation { Instance: null or IInstanceReferenceOperation } field => field.Field,
            _ => null,
        };

    public bool IsEmpty => _taints.Count == 0;

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

    // Every taint any of the states holds: a flow-insensitive summary.
    public static TaintState Union(IEnumerable<TaintState> states)
    {
        var union = new TaintState();
        foreach (var state in states)
        {
            state.MergeInto(union);
        }

        return union;
    }

    // Unions this state into `into` and reports whether anything there grew: a label, or a source
    // upgraded from a public parameter to an entry point (which happens at most once per key, so
    // the analysis still converges).
    public bool MergeInto(TaintState into)
    {
        var changed = false;
        foreach (var (key, taint) in _taints)
        {
            var old = into.Get(key);
            var merged = old.Join(taint);
            if (merged.Labels != old.Labels || merged.Source != old.Source)
            {
                into._taints[key] = merged;
                changed = true;
            }
        }

        return changed;
    }
}

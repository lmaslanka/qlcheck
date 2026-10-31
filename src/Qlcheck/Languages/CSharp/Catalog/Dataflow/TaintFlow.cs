// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// Follows untrusted data through one body's control-flow graph and records every sink it reaches.
// A forward may-analysis: the state maps locals, parameters and flow captures to their taint, a
// worklist runs the blocks to a fixpoint, and joins union the labels. Assigning a clean value kills
// the taint. A call the model does not know returns clean data (D5).
internal sealed class TaintFlow
{
    // The lattice is finite, so the worklist converges; the cap only guards against a bug or a
    // pathological body, which then gives no Findings rather than a timeout.
    private const int IterationsPerBlock = 50;

    private readonly IReadOnlyList<Sink> _sinks;

    private readonly Dictionary<(Sink Sink, SyntaxNode Node), SinkHit> _hits = [];

    private TaintFlow(IReadOnlyList<Sink> sinks)
    {
        _sinks = sinks;
    }

    public static IReadOnlyList<SinkHit> Run(BodyGraph body, IReadOnlyList<Sink> sinks)
    {
        var flow = new TaintFlow(sinks);
        return flow.Analyze(body.Graph, TaintModel.Seed(body.Owner)) ? [.. flow._hits.Values] : [];
    }

    private static object? KeyOf(IOperation operation) =>
        operation switch
        {
            ILocalReferenceOperation local => local.Local,
            IParameterReferenceOperation parameter => parameter.Parameter,
            IFlowCaptureReferenceOperation capture => capture.Id,
            _ => null,
        };

    private bool Analyze(ControlFlowGraph graph, TaintState entry)
    {
        var states = new Dictionary<BasicBlock, TaintState> { [graph.Blocks[0]] = entry };
        var work = new Queue<BasicBlock>([graph.Blocks[0]]);
        var budget = IterationsPerBlock * graph.Blocks.Length;
        while (work.Count > 0 || EnterUnvisited(graph, states, work))
        {
            if (--budget < 0)
            {
                return false;
            }

            var block = work.Dequeue();
            var state = states[block].Clone();
            foreach (var operation in block.Operations)
            {
                Eval(operation, state);
            }

            if (block.BranchValue is { } branch)
            {
                Eval(branch, state);
            }

            Flow(block.ConditionalSuccessor, state, states, work);
            Flow(block.FallThroughSuccessor, state, states, work);
        }

        return true;
    }

    // Finally and catch blocks are entered through exception-handling regions, not branches. Once
    // the worklist drains, each reachable block not yet visited starts from the union of every
    // state seen so far: imprecise, but it errs toward reporting a hit in a handler.
    private static bool EnterUnvisited(ControlFlowGraph graph, Dictionary<BasicBlock, TaintState> states, Queue<BasicBlock> work)
    {
        var block = graph.Blocks.FirstOrDefault(candidate => candidate.IsReachable && !states.ContainsKey(candidate));
        if (block is null)
        {
            return false;
        }

        var union = new TaintState();
        foreach (var state in states.Values)
        {
            state.MergeInto(union);
        }

        states[block] = union;
        work.Enqueue(block);
        return true;
    }

    private static void Flow(ControlFlowBranch? branch, TaintState state, Dictionary<BasicBlock, TaintState> states, Queue<BasicBlock> work)
    {
        if (branch?.Destination is not { } destination)
        {
            return;
        }

        if (!states.TryGetValue(destination, out var current))
        {
            states[destination] = state.Clone();
            work.Enqueue(destination);
            return;
        }

        if (state.MergeInto(current))
        {
            work.Enqueue(destination);
        }
    }

    private Taint Eval(IOperation operation, TaintState state) =>
        operation switch
        {
            IFlowCaptureOperation capture => Capture(capture, state),
            ILocalReferenceOperation or IParameterReferenceOperation or IFlowCaptureReferenceOperation => state.Get(KeyOf(operation)!),
            ILiteralOperation => Taint.None,
            IConversionOperation conversion => Convert(conversion.Type, Eval(conversion.Operand, state)),
            IBinaryOperation binary => Binary(binary, state),
            IInterpolatedStringOperation interpolated => Interpolated(interpolated, state),
            IInterpolatedStringHandlerCreationOperation handler => Eval(handler.Content, state),
            ISimpleAssignmentOperation assignment => Assign(assignment, state),
            IObjectCreationOperation creation => Create(creation, state),
            IInvocationOperation invocation => Invoke(invocation, state),
            _ => Children(operation, state),
        };

    private Taint Children(IOperation operation, TaintState state)
    {
        foreach (var child in operation.ChildOperations)
        {
            Eval(child, state);
        }

        return Taint.None;
    }

    private Taint Capture(IFlowCaptureOperation capture, TaintState state)
    {
        var taint = Eval(capture.Value, state);
        state.Set(capture.Id, taint);
        return taint;
    }

    // A conversion to a safe type ends the taint; to a numeric type it leaves only Numeric.
    private static Taint Convert(ITypeSymbol? type, Taint taint)
    {
        if (TaintModel.IsNumeric(type))
        {
            return taint.Relabel(taint.IsNone ? TaintLabel.None : TaintLabel.Numeric);
        }

        return TaintModel.IsSafe(type) ? Taint.None : taint;
    }

    private Taint Binary(IBinaryOperation binary, TaintState state)
    {
        var taint = Eval(binary.LeftOperand, state).Join(Eval(binary.RightOperand, state));
        if (binary.Type?.SpecialType == SpecialType.System_String)
        {
            return taint;
        }

        return TaintModel.IsNumeric(binary.Type) ? Convert(binary.Type, taint) : Taint.None;
    }

    private Taint Interpolated(IInterpolatedStringOperation interpolated, TaintState state)
    {
        var taint = Taint.None;
        foreach (var part in interpolated.Parts)
        {
            if (part is IInterpolationOperation hole && !TaintModel.IsSafe(hole.Expression.Type))
            {
                taint = taint.Join(Eval(hole.Expression, state));
            }
        }

        return taint;
    }

    private Taint Assign(ISimpleAssignmentOperation assignment, TaintState state)
    {
        var taint = Eval(assignment.Value, state);
        switch (assignment.Target)
        {
            case ILocalReferenceOperation local:
                state.Set(local.Local, taint.Through(local.Local.Name));
                break;
            case IParameterReferenceOperation or IFlowCaptureReferenceOperation:
                state.Set(KeyOf(assignment.Target)!, taint);
                break;
            case IPropertyReferenceOperation property:
                if (property.Instance is { } instance)
                {
                    Eval(instance, state);
                }

                Check(property.Property, null, taint, assignment.Syntax);
                break;
            default:
                Eval(assignment.Target, state);
                break;
        }

        return taint;
    }

    // Constructor arguments can be sinks. Object initializers are lowered into separate property
    // assignments on a flow capture, so their sinks are checked by Assign.
    private Taint Create(IObjectCreationOperation creation, TaintState state)
    {
        foreach (var argument in creation.Arguments)
        {
            var taint = Eval(argument.Value, state);
            if (creation.Constructor is { } constructor)
            {
                Check(constructor, argument.Parameter?.Name, taint, creation.Syntax);
            }
        }

        if (creation.Initializer is { } initializer)
        {
            Eval(initializer, state);
        }

        return Taint.None;
    }

    private Taint Invoke(IInvocationOperation invocation, TaintState state)
    {
        if (invocation.Instance is { } instance)
        {
            Eval(instance, state);
        }

        foreach (var argument in invocation.Arguments)
        {
            Check(invocation.TargetMethod, argument.Parameter?.Name, Eval(argument.Value, state), invocation.Syntax);
        }

        return TaintModel.SourceOf(invocation.TargetMethod, invocation.Syntax) is { } source
            ? Taint.From(TaintModel.LabelFor(invocation.Type), source)
            : Taint.None;
    }

    private void Check(ISymbol member, string? parameter, Taint taint, SyntaxNode at)
    {
        if (taint is not { IsNone: false, Source: { } source })
        {
            return;
        }

        foreach (var sink in _sinks)
        {
            if (!sink.Matches(member, parameter))
            {
                continue;
            }

            var key = (sink, at);
            _hits[key] = _hits.TryGetValue(key, out var hit)
                ? hit with { Labels = hit.Labels | taint.Labels }
                : new SinkHit(sink, at, taint.Labels, source, taint.Path);
        }
    }
}

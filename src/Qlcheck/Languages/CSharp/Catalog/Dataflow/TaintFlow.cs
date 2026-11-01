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

    // Receivers whose FileName was set to a constant shell, and the tainted property sets whose
    // sink depends on whether their receiver turns out to be one. Both ignore control flow.
    private readonly HashSet<object> _shells = [];

    private readonly List<(Sink Sink, SyntaxNode Node, string Target, Taint Taint, object? Receiver)> _programCandidates = [];

    // Flow captures that stand for a local or parameter rather than its value: the compiler lowers
    // `x ??= e` into an assignment through such a capture, which must write to x.
    private readonly Dictionary<CaptureId, ILocalSymbol> _localAliases = [];

    // The taint passed to each local-function parameter at its call sites (D6), and a counter that
    // grows whenever one of them gains a label.
    private readonly Dictionary<IParameterSymbol, Taint> _localArguments = new(SymbolEqualityComparer.Default);

    private int _localArgumentsVersion;

    // Variables assigned from Path.GetFullPath, flow-insensitively, for the StartsWith guard.
    private readonly HashSet<object> _fullPaths = [];

    private TaintFlow(IReadOnlyList<Sink> sinks)
    {
        _sinks = sinks;
    }

    public static IReadOnlyList<SinkHit> Run(BodyGraph body, IReadOnlyList<Sink> sinks)
    {
        var flow = new TaintFlow(sinks);
        if (!flow.Analyze(body.Graph, TaintModel.Seed(body.Owner)))
        {
            return [];
        }

        foreach (var (sink, node, target, taint, receiver) in flow._programCandidates)
        {
            if (Expects(sink, receiver is not null && flow._shells.Contains(receiver)))
            {
                flow.Record(sink, node, target, taint);
            }
        }

        return [.. flow._hits.Values];
    }

    // The object a property is set on, as a key: a local, parameter or flow capture, or a property
    // path from one (`p.StartInfo`). Null when it cannot be named.
    private static object? ReceiverKey(IOperation? operation) =>
        operation switch
        {
            IPropertyReferenceOperation property => ReceiverKey(property.Instance) is { } inner ? (inner, property.Property.Name) : null,
            IConversionOperation conversion => ReceiverKey(conversion.Operand),
            null => null,
            _ => TaintState.KeyOf(operation),
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

            var jumpsWhen = block.ConditionKind == ControlFlowConditionKind.WhenTrue;
            Flow(block.ConditionalSuccessor, Guarded(block, state, holds: jumpsWhen), states, work);
            Flow(block.FallThroughSuccessor, Guarded(block, state, holds: !jumpsWhen), states, work);
        }

        return AnalyzeNested(graph, TaintState.Union(states.Values));
    }

    // Lambdas and local functions are sub-graphs (D6). Each starts from everything the enclosing
    // body's variables ever held (flow-insensitive capture); a local function's parameters also
    // hold what its calls passed. A call from one local function to another can pass more, so the
    // local functions run again until their parameters stop growing.
    private bool AnalyzeNested(ControlFlowGraph graph, TaintState outer)
    {
        foreach (var lambda in Lambdas(graph))
        {
            if (!Analyze(graph.GetAnonymousFunctionControlFlowGraph(lambda), outer.Clone()))
            {
                return false;
            }
        }

        for (var round = 0; round <= graph.LocalFunctions.Length; round++)
        {
            var version = _localArgumentsVersion;
            foreach (var function in graph.LocalFunctions)
            {
                var entry = outer.Clone();
                foreach (var parameter in function.Parameters)
                {
                    entry.Set(parameter, _localArguments.GetValueOrDefault(parameter));
                }

                if (!Analyze(graph.GetLocalFunctionControlFlowGraph(function), entry))
                {
                    return false;
                }
            }

            if (version == _localArgumentsVersion)
            {
                break;
            }
        }

        return true;
    }

    private static IEnumerable<IFlowAnonymousFunctionOperation> Lambdas(ControlFlowGraph graph) =>
        graph.Blocks
            .SelectMany(block => block.BranchValue is null ? block.Operations : block.Operations.Add(block.BranchValue))
            .SelectMany(operation => operation.DescendantsAndSelf())
            .OfType<IFlowAnonymousFunctionOperation>();

    // The state on the edge where the block's branch condition is `holds`.
    private TaintState Guarded(BasicBlock block, TaintState state, bool holds)
    {
        if (block.ConditionKind == ControlFlowConditionKind.None || block.BranchValue is null)
        {
            return state;
        }

        var guarded = state.Clone();
        TaintGuards.Apply(block.BranchValue, holds, guarded, _fullPaths);
        return guarded;
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

        states[block] = TaintState.Union(states.Values);
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
            ILocalReferenceOperation or IParameterReferenceOperation or IFlowCaptureReferenceOperation => state.Get(TaintState.KeyOf(operation)!),
            IFieldReferenceOperation field when TaintState.KeyOf(field) is { } key => state.Get(key),
            ILiteralOperation => Taint.None,
            IConversionOperation conversion => TaintModel.Retype(conversion.Type, Eval(conversion.Operand, state)),
            IBinaryOperation binary => Binary(binary, state),
            IInterpolatedStringOperation interpolated => Interpolated(interpolated, state),
            IInterpolatedStringHandlerCreationOperation handler => Eval(handler.Content, state),
            ISimpleAssignmentOperation assignment => Assign(assignment, state),
            ICompoundAssignmentOperation compound => Compound(compound, state),
            IObjectCreationOperation creation => Create(creation, state),
            IArrayCreationOperation array => Array(array, state),
            IArrayElementReferenceOperation element => Element(element, state),
            IPropertyReferenceOperation property => Read(property, state),
            IArrayInitializerOperation initializer => Elements(initializer, state),
            IInvocationOperation invocation => Invoke(invocation, state),
            IAwaitOperation awaited => TaintModel.Retype(awaited.Type, Eval(awaited.Operation, state)),
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
        if (capture.Value is ILocalReferenceOperation local)
        {
            _localAliases[capture.Id] = local.Local;
        }

        var taint = Eval(capture.Value, state);
        state.Set(capture.Id, taint);
        return taint;
    }

    private Taint Binary(IBinaryOperation binary, TaintState state) =>
        Combined(binary.Type, Eval(binary.LeftOperand, state).Join(Eval(binary.RightOperand, state)));

    // The result of an operator over tainted operands: a string keeps the taint, a number keeps
    // only Numeric, and anything else (a comparison, say) is clean.
    private static Taint Combined(ITypeSymbol? type, Taint taint)
    {
        if (type?.SpecialType == SpecialType.System_String)
        {
            return taint;
        }

        return TaintModel.IsNumeric(type) ? TaintModel.Retype(type, taint) : Taint.None;
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
        Store(assignment, taint, state);
        return taint;
    }

    // `x += e` keeps what x held and adds e. A sink property set this way is still a sink.
    private Taint Compound(ICompoundAssignmentOperation compound, TaintState state)
    {
        var taint = Combined(compound.Type, Eval(compound.Target, state).Join(Eval(compound.Value, state)));
        Store(compound, taint, state);
        return taint;
    }

    private void Store(IAssignmentOperation assignment, Taint taint, TaintState state)
    {
        switch (assignment.Target)
        {
            case ILocalReferenceOperation local:
                state.Set(local.Local, taint.Through(local.Local.Name));
                if (TaintModel.IsFullPath(assignment.Value))
                {
                    _fullPaths.Add(local.Local);
                }

                break;
            case IFlowCaptureReferenceOperation capture when _localAliases.TryGetValue(capture.Id, out var aliased):
                state.Set(capture.Id, taint);
                state.Set(aliased, taint.Through(aliased.Name));
                break;
            case var target when TaintState.KeyOf(target) is { } key:
                state.Set(key, taint);
                break;
            case IPropertyReferenceOperation property:
                SetProperty(property, assignment, taint, state);
                break;
            default:
                Eval(assignment.Target, state);
                break;
        }
    }

    private void SetProperty(IPropertyReferenceOperation property, IAssignmentOperation assignment, Taint taint, TaintState state)
    {
        if (property.Instance is { } instance)
        {
            Eval(instance, state);
        }

        foreach (var argument in property.Arguments)
        {
            Eval(argument.Value, state);
        }

        // `map[key] = value` puts the value's taint into the collection.
        if (property.Property.IsIndexer && Root(property.Instance) is { } root)
        {
            Store(root, state.Get(root).Join(taint), state);
        }

        var receiver = ReceiverKey(property.Instance);
        if (receiver is not null && TaintModel.IsProgram(property.Property) && TaintModel.IsShell(assignment.Value.ConstantValue))
        {
            _shells.Add(receiver);
        }

        Check(property.Property, null, taint, assignment.Syntax, shell: null, receiver: receiver);
    }

    // Constructor arguments can be sinks. Object initializers are lowered into separate property
    // assignments on a flow capture, so their sinks are checked by Assign. A string, StringBuilder
    // or Uri built from tainted arguments is tainted.
    private Taint Create(IObjectCreationOperation creation, TaintState state)
    {
        var shell = TaintModel.StartsShell(creation.Arguments);
        var arguments = Taint.None;
        foreach (var argument in creation.Arguments)
        {
            var taint = Eval(argument.Value, state);
            arguments = arguments.Join(taint);
            if (creation.Constructor is { } constructor)
            {
                Check(constructor, argument.Parameter?.Name, taint, creation.Syntax, shell);
            }
        }

        if (creation.Initializer is { } initializer)
        {
            Eval(initializer, state);
        }

        return TaintModel.CarriesTaint(creation.Type) ? arguments : Taint.None;
    }

    // An array holds the taint of its elements.
    private Taint Array(IArrayCreationOperation array, TaintState state)
    {
        foreach (var size in array.DimensionSizes)
        {
            Eval(size, state);
        }

        return array.Initializer is { } initializer ? Elements(initializer, state) : Taint.None;
    }

    private Taint Element(IArrayElementReferenceOperation element, TaintState state)
    {
        foreach (var index in element.Indices)
        {
            Eval(index, state);
        }

        return Eval(element.ArrayReference, state);
    }

    // Reading an element (an indexer, or an enumerator's Current) gives the collection's taint; any
    // other property read is clean.
    private Taint Read(IPropertyReferenceOperation property, TaintState state)
    {
        var instance = property.Instance is { } receiver ? Eval(receiver, state) : Taint.None;
        foreach (var argument in property.Arguments)
        {
            Eval(argument.Value, state);
        }

        return property.Property.IsIndexer || property.Property.Name == nameof(IEnumerator<object>.Current) ? TaintModel.Retype(property.Type, instance) : Taint.None;
    }

    private Taint Elements(IArrayInitializerOperation initializer, TaintState state)
    {
        var taint = Taint.None;
        foreach (var element in initializer.ElementValues)
        {
            taint = taint.Join(Eval(element, state));
        }

        return taint;
    }

    private Taint Invoke(IInvocationOperation invocation, TaintState state)
    {
        var method = invocation.TargetMethod;
        var receiver = invocation.Instance is { } instance ? Eval(instance, state) : Taint.None;
        var taints = Arguments(invocation, state);
        var arguments = taints.Aggregate(Taint.None, (all, taint) => all.Join(taint));
        if (TaintModel.SourceOf(invocation) is { } origin)
        {
            return Taint.From(TaintModel.LabelFor(invocation.Type), origin);
        }

        if (TaintModel.Sanitize(invocation, InputOf(invocation, taints)) is { } sanitized)
        {
            StoreParsed(invocation, InputOf(invocation, taints), state);
            return sanitized;
        }

        if (TaintModel.IsLinq(method))
        {
            return TaintModel.Retype(invocation.Type, taints.FirstOrDefault());
        }

        if (TaintModel.MutatesReceiver(method) && Root(invocation.Instance) is { } root)
        {
            Store(root, state.Get(root).Join(arguments), state);
        }

        return TaintModel.Propagates(method) ? TaintModel.Retype(invocation.Type, receiver.Join(arguments)) : Taint.None;
    }

    // Each argument's taint, after checking it against the sinks and passing it to a local
    // function's parameter.
    private List<Taint> Arguments(IInvocationOperation invocation, TaintState state)
    {
        var method = invocation.TargetMethod;
        var shell = TaintModel.StartsShell(invocation.Arguments);
        var taints = new List<Taint>(invocation.Arguments.Length);
        foreach (var argument in invocation.Arguments)
        {
            var taint = Eval(argument.Value, state);
            taints.Add(taint);
            Check(method, argument.Parameter?.Name, taint, invocation.Syntax, shell, instance: invocation.Instance);
            if (method.MethodKind == MethodKind.LocalFunction && argument.Parameter is { } parameter)
            {
                PassToLocalFunction(parameter.OriginalDefinition, taint);
            }
        }

        return taints;
    }

    // The taint of the arguments a call reads, leaving out `out` arguments it only writes.
    private static Taint InputOf(IInvocationOperation invocation, List<Taint> taints) =>
        invocation.Arguments
            .Select((argument, index) => argument.Parameter?.RefKind == RefKind.Out ? Taint.None : taints[index])
            .Aggregate(Taint.None, (all, taint) => all.Join(taint));

    // `int.TryParse(s, out var n)`: n gets what parsing s leaves.
    private static void StoreParsed(IInvocationOperation invocation, Taint input, TaintState state)
    {
        if (!TaintModel.IsParse(invocation.TargetMethod))
        {
            return;
        }

        foreach (var argument in invocation.Arguments.Where(argument => argument.Parameter?.RefKind == RefKind.Out))
        {
            var target = argument.Value is IDeclarationExpressionOperation declaration ? declaration.Expression : argument.Value;
            if (TaintState.KeyOf(target) is { } key)
            {
                Store(key, TaintModel.Retype(argument.Parameter!.Type, input), state);
            }
        }
    }

    private void PassToLocalFunction(IParameterSymbol parameter, Taint taint)
    {
        var old = _localArguments.GetValueOrDefault(parameter);
        var joined = old.Join(taint);
        if (joined.Labels != old.Labels)
        {
            _localArguments[parameter] = joined;
            _localArgumentsVersion++;
        }
    }

    // The local, parameter or capture a call chain starts from: `builder` in
    // `builder.Append(a).Append(b)`.
    private static object? Root(IOperation? operation) =>
        operation switch
        {
            IInvocationOperation { Instance: { } instance } => Root(instance),
            IConversionOperation conversion => Root(conversion.Operand),
            null => null,
            _ => TaintState.KeyOf(operation),
        };

    private static void Store(object key, Taint taint, TaintState state) =>
        state.Set(key, key is ILocalSymbol local ? taint.Through(local.Name) : taint);

    // `shell` says whether the call starts a constant shell. For a property set it is not known
    // yet (null), so a sink that depends on the program waits on the receiver instead. `instance`
    // is the call's receiver, for sinks that name one.
    private void Check(ISymbol member, string? parameter, Taint taint, SyntaxNode at, bool? shell, object? receiver = null, IOperation? instance = null)
    {
        if (taint.IsNone)
        {
            return;
        }

        var target = Target(member, parameter);
        var matching = _sinks.Where(sink => sink.Matches(member, parameter) && sink.ReceiverMatches(instance)).ToList();
        foreach (var sink in matching.Where(sink => sink.Program == ProgramKind.Any || shell is { } known && Expects(sink, known)))
        {
            Record(sink, at, target, taint);
        }

        if (shell is null)
        {
            _programCandidates.AddRange(matching.Where(sink => sink.Program != ProgramKind.Any).Select(sink => (sink, at, target, taint, receiver)));
        }
    }

    private static bool Expects(Sink sink, bool shell) =>
        sink.Program switch
        {
            ProgramKind.Shell => shell,
            ProgramKind.NotShell => !shell,
            _ => true,
        };

    private static string Target(ISymbol member, string? parameter) =>
        member switch
        {
            IMethodSymbol { MethodKind: MethodKind.Constructor } constructor => $"new {constructor.ContainingType.Name}({parameter})",
            IMethodSymbol method => $"{method.ContainingType.Name}.{method.Name}({parameter})",
            _ => $"{member.ContainingType.Name}.{member.Name}",
        };

    private void Record(Sink sink, SyntaxNode at, string target, Taint taint)
    {
        if (taint.Source is not { } source)
        {
            return;
        }

        var key = (sink, at);
        _hits[key] = _hits.TryGetValue(key, out var hit)
            ? hit with { Labels = hit.Labels | taint.Labels }
            : new SinkHit(sink, at, target, taint.Labels, source, taint.Path);
    }
}

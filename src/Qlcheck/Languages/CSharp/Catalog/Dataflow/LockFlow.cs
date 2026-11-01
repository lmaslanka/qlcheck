// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// Which locks are held where, over each body's control-flow graph: a forward may-analysis of the
// set of held (lock, mode, acquisition) entries. Only normal paths count (D10): the releases in the
// finally regions a branch leaves through are applied on that branch, and exceptional paths (catch
// blocks, throws) are ignored. A conditional acquisition (TryEnter, a Wait or WaitOne used as a
// condition) holds only on the edge where it succeeded.
internal static class LockFlow
{
    private const int IterationsPerBlock = 50;

    private const string SemaphoreSlimType = "System.Threading.SemaphoreSlim";

    private const string SemaphoreType = "System.Threading.Semaphore";

    private static readonly (string Type, string Method, LockMode Mode, bool Conditional)[] Acquisitions =
    [
        ("System.Threading.Monitor", "Enter", LockMode.Exclusive, false),
        ("System.Threading.Monitor", "TryEnter", LockMode.Exclusive, true),
        ("System.Threading.ReaderWriterLockSlim", "EnterReadLock", LockMode.Read, false),
        ("System.Threading.ReaderWriterLockSlim", "EnterWriteLock", LockMode.Write, false),
        ("System.Threading.ReaderWriterLockSlim", "EnterUpgradeableReadLock", LockMode.Upgradeable, false),
        ("System.Threading.ReaderWriterLockSlim", "TryEnterReadLock", LockMode.Read, true),
        ("System.Threading.ReaderWriterLockSlim", "TryEnterWriteLock", LockMode.Write, true),
        ("System.Threading.ReaderWriterLockSlim", "TryEnterUpgradeableReadLock", LockMode.Upgradeable, true),
        ("System.Threading.ReaderWriterLock", "AcquireReaderLock", LockMode.Read, false),
        ("System.Threading.ReaderWriterLock", "AcquireWriterLock", LockMode.Write, false),
        ("System.Threading.SpinLock", "Enter", LockMode.Exclusive, false),
        ("System.Threading.WaitHandle", "WaitOne", LockMode.Exclusive, false),
        (SemaphoreSlimType, "Wait|WaitAsync", LockMode.Exclusive, false),
    ];

    private static readonly (string Type, string Method, LockMode Mode)[] Releases =
    [
        ("System.Threading.Monitor", "Exit", LockMode.Exclusive),
        ("System.Threading.ReaderWriterLockSlim", "ExitReadLock", LockMode.Read),
        ("System.Threading.ReaderWriterLockSlim", "ExitWriteLock", LockMode.Write),
        ("System.Threading.ReaderWriterLockSlim", "ExitUpgradeableReadLock", LockMode.Upgradeable),
        ("System.Threading.ReaderWriterLock", "ReleaseReaderLock", LockMode.Read),
        ("System.Threading.ReaderWriterLock", "ReleaseWriterLock", LockMode.Write),
        ("System.Threading.SpinLock", "Exit", LockMode.Exclusive),
        ("System.Threading.Mutex", "ReleaseMutex", LockMode.Exclusive),
        (SemaphoreSlimType, "Release", LockMode.Exclusive),
        (SemaphoreType, "Release", LockMode.Exclusive),
    ];

    // The identifiers a body must contain to acquire a lock.
    public static IReadOnlySet<string> Names { get; } =
        Acquisitions.SelectMany(acquisition => acquisition.Method.Split('|')).ToHashSet(StringComparer.Ordinal);

    public static IEnumerable<LockHit> Run(BodyGraphs graphs) =>
        graphs.Where((_, tokens) => tokens.Any(token => Names.Contains(token.ValueText)))
            .SelectMany(body => Analyze(body.Graph))
            .Distinct();

    private static IEnumerable<LockHit> Analyze(ControlFlowGraph graph)
    {
        var walk = new Walk(graph);
        var nested = graph.LocalFunctions.Select(function => graph.GetLocalFunctionControlFlowGraph(function))
            .Concat(graph.Blocks.SelectMany(block => block.Operations).SelectMany(operation => operation.DescendantsAndSelf())
                .OfType<IFlowAnonymousFunctionOperation>().Select(lambda => graph.GetAnonymousFunctionControlFlowGraph(lambda)));
        return walk.Run().Concat(nested.SelectMany(Analyze));
    }

    // One graph's analysis.
    private sealed class Walk(ControlFlowGraph graph)
    {
        private readonly Dictionary<BasicBlock, HashSet<Held>> _states = [];

        private readonly HashSet<Held> _heldAtExit = [];

        private readonly HashSet<object> _released = [];

        private readonly List<LockHit> _mismatches = [];

        private static bool IsSemaphore(IInvocationOperation invocation) =>
            Symbols.Is(invocation.TargetMethod.ContainingType, SemaphoreSlimType) || Symbols.Is(invocation.Instance?.Type, SemaphoreType);

        // The lock an invocation acts on: Monitor's first argument, or the receiver of an instance call.
        private static (object Key, LockMode Mode, bool Conditional)? Match(IOperation operation, IEnumerable<(string Type, string Method, LockMode Mode, bool Conditional)> table)
        {
            if (Unwrap(operation) is not IInvocationOperation invocation)
            {
                return null;
            }

            var method = invocation.TargetMethod;
            foreach (var (type, name, mode, conditional) in table)
            {
                if (name.Split('|').Contains(method.Name) && Symbols.Is(method.ContainingType, type) && LockOf(invocation) is { } key)
                {
                    return (key, mode, conditional || method.ReturnType.SpecialType == SpecialType.System_Boolean);
                }
            }

            return null;
        }

        private static object? LockOf(IInvocationOperation invocation)
        {
            var target = invocation.Instance ?? (invocation.Arguments.Length > 0 ? invocation.Arguments[0].Value : null);
            return target is null ? null : TaintState.KeyOf(Unwrap(target));
        }

        private static IOperation Unwrap(IOperation operation) =>
            operation switch
            {
                IConversionOperation conversion => Unwrap(conversion.Operand),
                IAwaitOperation awaited => Unwrap(awaited.Operation),
                IExpressionStatementOperation statement => Unwrap(statement.Operation),
                _ => operation,
            };

        private static (object Key, LockMode Mode, bool Conditional)? AcquisitionOf(IOperation operation) =>
            Match(operation, Acquisitions.Select(entry => (entry.Type, entry.Method, entry.Mode, entry.Conditional)));

        private static (object Key, LockMode Mode, bool Conditional)? ReleaseOf(IOperation operation) =>
            Match(operation, Releases.Select(entry => (entry.Type, entry.Method, entry.Mode, false)));

        private static string Describe(IInvocationOperation invocation)
        {
            var target = invocation.Instance ?? invocation.Arguments[0].Value;
            return Unwrap(target).Syntax.ToString();
        }

        public IEnumerable<LockHit> Run()
        {
            CollectReleases();
            var work = new Queue<BasicBlock>([graph.Blocks[0]]);
            _states[graph.Blocks[0]] = [];
            var budget = IterationsPerBlock * graph.Blocks.Length;
            while (work.Count > 0 && --budget > 0)
            {
                var block = work.Dequeue();
                var state = new HashSet<Held>(_states[block]);
                foreach (var operation in block.Operations.SelectMany(operation => operation.DescendantsAndSelf()))
                {
                    Apply(operation, state);
                }

                var jumpsWhen = block.ConditionKind == ControlFlowConditionKind.WhenTrue;
                Follow(block, block.ConditionalSuccessor, state, holds: jumpsWhen, work);
                Follow(block, block.FallThroughSuccessor, state, holds: !jumpsWhen, work);
            }

            if (budget <= 0)
            {
                return [];
            }

            // A semaphore is a counter, often waited on in one method and released in another
            // (a throttle), so only a path that misses the method's own release counts for it.
            var held = _heldAtExit
                .Where(entry => _released.Contains(entry.Key) || !entry.Counting)
                .Select(entry => new LockHit(_released.Contains(entry.Key) ? LockProblem.NotReleasedOnAllPaths : LockProblem.NeverReleased, entry.At, entry.Name));
            return held.Concat(_mismatches);
        }

        // Every lock released anywhere in the graph, finally regions included.
        private void CollectReleases() =>
            _released.UnionWith(graph.Blocks.SelectMany(block => block.Operations)
                .SelectMany(operation => operation.DescendantsAndSelf())
                .Select(operation => ReleaseOf(operation)?.Key)
                .OfType<object>());

        private void Follow(BasicBlock from, ControlFlowBranch? branch, HashSet<Held> state, bool holds, Queue<BasicBlock> work)
        {
            if (branch is null || branch.Semantics is ControlFlowBranchSemantics.Throw or ControlFlowBranchSemantics.Rethrow)
            {
                return;
            }

            var next = new HashSet<Held>(state);
            if (from.ConditionKind != ControlFlowConditionKind.None && from.BranchValue is { } condition)
            {
                AcquireIf(condition, holds, next);
            }

            LeaveFinallyRegions(branch, next);
            if (branch.Destination is not { } destination)
            {
                return;
            }

            if (destination.Kind == BasicBlockKind.Exit)
            {
                _heldAtExit.UnionWith(next);
                return;
            }

            Merge(destination, next, work);
        }

        // The releases in each finally region the branch leaves through (D10: normal paths only).
        private void LeaveFinallyRegions(ControlFlowBranch branch, HashSet<Held> state)
        {
            var operations = branch.FinallyRegions
                .SelectMany(region => graph.Blocks.Skip(region.FirstBlockOrdinal).Take(region.LastBlockOrdinal - region.FirstBlockOrdinal + 1))
                .SelectMany(block => block.Operations)
                .SelectMany(operation => operation.DescendantsAndSelf());
            foreach (var operation in operations)
            {
                Apply(operation, state, acquires: false);
            }
        }

        private void Merge(BasicBlock destination, HashSet<Held> next, Queue<BasicBlock> work)
        {
            if (!_states.TryGetValue(destination, out var current))
            {
                _states[destination] = next;
                work.Enqueue(destination);
                return;
            }

            var before = current.Count;
            current.UnionWith(next);
            if (current.Count != before)
            {
                work.Enqueue(destination);
            }
        }

        // `if (Monitor.TryEnter(x))`: the lock is held on the edge where the call returned true.
        private static void AcquireIf(IOperation condition, bool holds, HashSet<Held> state)
        {
            if (condition is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } not)
            {
                AcquireIf(not.Operand, !holds, state);
                return;
            }

            if (holds && AcquisitionOf(condition) is { Conditional: true } acquisition && Unwrap(condition) is IInvocationOperation invocation)
            {
                state.Add(new Held(acquisition.Key, acquisition.Mode, invocation.Syntax, Describe(invocation), IsSemaphore(invocation)));
            }
        }

        // An unconditional acquisition adds the lock; a release removes it in its mode, and is a
        // mismatch when the lock is held only in another reader-writer mode. Finally regions only
        // release.
        private void Apply(IOperation operation, HashSet<Held> state, bool acquires = true)
        {
            if (acquires && AcquisitionOf(operation) is { Conditional: false } acquisition && operation is IInvocationOperation acquire)
            {
                state.Add(new Held(acquisition.Key, acquisition.Mode, acquire.Syntax, Describe(acquire), IsSemaphore(acquire)));
                return;
            }

            if (ReleaseOf(operation) is not { } release || operation is not IInvocationOperation call)
            {
                return;
            }

            var ofLock = state.Where(entry => Equals(entry.Key, release.Key)).ToList();
            var matching = ofLock.Where(entry => entry.Mode == release.Mode).ToList();
            if (matching.Count == 0 && ofLock.Count > 0 && release.Mode != LockMode.Exclusive)
            {
                _mismatches.Add(new LockHit(LockProblem.ModeMismatch, call.Syntax, Describe(call)));
            }

            state.ExceptWith(matching);
        }
    }

    private sealed record Held(object Key, LockMode Mode, SyntaxNode At, string Name, bool Counting);
}

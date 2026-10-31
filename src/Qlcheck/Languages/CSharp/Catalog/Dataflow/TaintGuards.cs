// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// Edge guards (D7): a branch condition that, where it holds, proves something about a value. On
// that edge only, the value is clean (equal to a constant, in a fixed set, a local URL), loses
// Numeric (bounded above by a constant), or becomes a ContainedPath (StartsWith after GetFullPath). `&&` and `||` are lowered into separate branches, so an
// allow-list chain such as `a != "ls" && a != "pwd"` cleans each comparison in turn.
internal static class TaintGuards
{
    private const string ContainsMethod = "Contains";

    private const string StartsWithMethod = "StartsWith";

    private const string LocalUrlMethod = "IsLocalUrl";

    private const string UrlHelperType = "Microsoft.AspNetCore.Mvc.IUrlHelper";

    private const TaintLabel Uncontained = TaintLabel.Untrusted | TaintLabel.ArchiveEntry;

    // Applies `condition` to `state` on the edge where the condition is `holds`. `fullPaths` are
    // the variables assigned from Path.GetFullPath.
    public static void Apply(IOperation condition, bool holds, TaintState state, IReadOnlySet<object> fullPaths)
    {
        switch (condition)
        {
            case IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } not:
                Apply(not.Operand, !holds, state, fullPaths);
                break;
            case IBinaryOperation binary when IsEquality(binary, holds):
                CleanIfConstant(binary.LeftOperand, binary.RightOperand, state);
                CleanIfConstant(binary.RightOperand, binary.LeftOperand, state);
                break;
            case IBinaryOperation binary when UpperBounded(binary, holds) is { } bounded:
                Relabel(bounded, state, labels => labels & ~TaintLabel.Numeric);
                break;
            case IInvocationOperation { TargetMethod.Name: ContainsMethod, Instance: { } set, Arguments: [var item] } when holds && IsFixedSet(set):
                Clean(item.Value, state);
                break;
            case IInvocationOperation { TargetMethod.Name: StartsWithMethod, Instance: { } path } when holds && TaintState.KeyOf(Unwrap(path)) is { } key && fullPaths.Contains(key):
                Relabel(path, state, Contain);
                break;
            case IInvocationOperation { TargetMethod: { Name: LocalUrlMethod } method, Arguments: [var url, ..] } when holds && Symbols.Is(method.ContainingType, UrlHelperType):
                Clean(url.Value, state);
                break;
            default:
                // Any other condition proves nothing about the values it reads.
                break;
        }
    }

    // The operand a relational comparison bounds above by a constant on this edge: `x < 10` or
    // `x <= 10` that holds, `x > 10` or `x >= 10` that fails, and the same with the constant first.
    private static IOperation? UpperBounded(IBinaryOperation binary, bool holds)
    {
        var below = binary.OperatorKind switch
        {
            BinaryOperatorKind.LessThan or BinaryOperatorKind.LessThanOrEqual => holds,
            BinaryOperatorKind.GreaterThan or BinaryOperatorKind.GreaterThanOrEqual => !holds,
            _ => (bool?)null,
        };

        return below switch
        {
            true when binary.RightOperand.ConstantValue.HasValue => binary.LeftOperand,
            false when binary.LeftOperand.ConstantValue.HasValue => binary.RightOperand,
            _ => null,
        };
    }

    // A path that starts with the base directory after GetFullPath is contained in it.
    private static TaintLabel Contain(TaintLabel labels) =>
        (labels & Uncontained) == TaintLabel.None ? labels : (labels & ~Uncontained) | TaintLabel.ContainedPath;

    private static void Relabel(IOperation value, TaintState state, Func<TaintLabel, TaintLabel> change)
    {
        if (TaintState.KeyOf(Unwrap(value)) is { } key)
        {
            var taint = state.Get(key);
            state.Set(key, taint.Relabel(change(taint.Labels)));
        }
    }

    // `==` where the condition holds, or `!=` where it fails: the operands are equal on this edge.
    private static bool IsEquality(IBinaryOperation binary, bool holds) =>
        binary.OperatorKind switch
        {
            BinaryOperatorKind.Equals => holds,
            BinaryOperatorKind.NotEquals => !holds,
            _ => false,
        };

    private static void CleanIfConstant(IOperation value, IOperation other, TaintState state)
    {
        if (other.ConstantValue.HasValue)
        {
            Clean(value, state);
        }
    }

    // A collection the attacker cannot add to: a readonly or const field.
    private static bool IsFixedSet(IOperation set) =>
        Unwrap(set) is IFieldReferenceOperation { Field: { IsReadOnly: true } or { IsConst: true } };

    private static void Clean(IOperation value, TaintState state)
    {
        if (TaintState.KeyOf(Unwrap(value)) is { } key)
        {
            state.Set(key, Taint.None);
        }
    }

    private static IOperation Unwrap(IOperation operation) =>
        operation is IConversionOperation conversion ? Unwrap(conversion.Operand) : operation;
}

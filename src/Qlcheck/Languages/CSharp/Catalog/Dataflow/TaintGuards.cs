// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Qlcheck.Languages.CSharp.Catalog.Dataflow;

// Edge guards (D7): a branch condition that, where it holds, proves a value is one the code chose.
// On that edge only, the value is clean. `&&` and `||` are lowered into separate branches, so an
// allow-list chain such as `a != "ls" && a != "pwd"` cleans each comparison in turn.
internal static class TaintGuards
{
    private const string ContainsMethod = "Contains";

    // Applies `condition` to `state` on the edge where the condition is `holds`.
    public static void Apply(IOperation condition, bool holds, TaintState state)
    {
        switch (condition)
        {
            case IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } not:
                Apply(not.Operand, !holds, state);
                break;
            case IBinaryOperation binary when IsEquality(binary, holds):
                CleanIfConstant(binary.LeftOperand, binary.RightOperand, state);
                CleanIfConstant(binary.RightOperand, binary.LeftOperand, state);
                break;
            case IInvocationOperation { TargetMethod.Name: ContainsMethod, Instance: { } set, Arguments: [var item] } when holds && IsFixedSet(set):
                Clean(item.Value, state);
                break;
            default:
                // Any other condition proves nothing about the values it reads.
                break;
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

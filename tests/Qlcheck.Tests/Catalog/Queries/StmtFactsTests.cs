using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Tests;

public class StmtFactsTests
{
    [Fact]
    public void NeedsBraces_is_true_for_braceless_for_loop()
    {
        var node = FirstNode<ForStatementSyntax>(
            "class C { void M() { for (var i = 0; i < 1; i++) System.Console.WriteLine(); } }");
        Assert.True(StmtFacts.NeedsBraces(node));
    }

    [Fact]
    public void NeedsBraces_is_true_for_braceless_foreach_loop()
    {
        var node = FirstNode<ForEachStatementSyntax>(
            "class C { void M() { foreach (var i in new int[0]) System.Console.WriteLine(); } }");
        Assert.True(StmtFacts.NeedsBraces(node));
    }

    [Fact]
    public void NeedsBraces_is_true_for_braceless_while_loop()
    {
        var node = FirstNode<WhileStatementSyntax>(
            "class C { void M(bool b) { while (b) System.Console.WriteLine(); } }");
        Assert.True(StmtFacts.NeedsBraces(node));
    }

    [Fact]
    public void NeedsBraces_is_true_for_braceless_do_loop()
    {
        var node = FirstNode<DoStatementSyntax>(
            "class C { void M(bool b) { do System.Console.WriteLine(); while (b); } }");
        Assert.True(StmtFacts.NeedsBraces(node));
    }

    [Fact]
    public void NeedsBraces_is_true_for_braceless_else()
    {
        var node = FirstNode<ElseClauseSyntax>(
            "class C { void M(bool b) { if (b) { } else System.Console.WriteLine(); } }");
        Assert.True(StmtFacts.NeedsBraces(node));
    }

    [Fact]
    public void NeedsBraces_is_true_for_braceless_lock()
    {
        var node = FirstNode<LockStatementSyntax>(
            "class C { void M(object o) { lock (o) System.Console.WriteLine(); } }");
        Assert.True(StmtFacts.NeedsBraces(node));
    }

    [Fact]
    public void NeedsBraces_is_true_for_braceless_using()
    {
        var node = FirstNode<UsingStatementSyntax>(
            "class C { void M(System.IDisposable d) { using (d) System.Console.WriteLine(); } }");
        Assert.True(StmtFacts.NeedsBraces(node));
    }

    [Fact]
    public void NeedsBraces_is_true_for_braceless_fixed()
    {
        var node = FirstNode<FixedStatementSyntax>(
            "unsafe class C { void M(int[] arr) { fixed (int* p = arr) System.Console.WriteLine(); } }");
        Assert.True(StmtFacts.NeedsBraces(node));
    }

    [Fact]
    public void RedundantParen_is_false_for_non_parenthesized_node()
    {
        Assert.False(StmtFacts.RedundantParen(Expression("1 + 1")));
    }

    [Fact]
    public void SelfAssignment_is_false_for_non_assignment_node()
    {
        Assert.False(StmtFacts.SelfAssignment(Expression("1")));
    }

    [Fact]
    public void SelfAssignment_is_true_for_a_bare_statement()
    {
        var node = FirstNode<AssignmentExpressionSyntax>(
            "class C { void M(int value) { value = value; } }");
        Assert.True(StmtFacts.SelfAssignment(node));
    }

    [Fact]
    public void SelfAssignment_is_false_inside_an_object_initializer()
    {
        var node = FirstNode<AssignmentExpressionSyntax>(
            "class Target { public int Id { get; set; } } "
            + "class C { public int Id { get; set; } "
            + "Target M() => new Target { Id = Id }; }");
        Assert.False(StmtFacts.SelfAssignment(node));
    }

    [Fact]
    public void IdenticalOperands_is_false_for_non_binary_node()
    {
        Assert.False(StmtFacts.IdenticalOperands(Expression("1")));
    }

    [Fact]
    public void LockOnLocal_is_false_for_non_lock_node()
    {
        Assert.False(StmtFacts.LockOnLocal(Expression("1")));
    }

    [Fact]
    public void LockOnLocal_is_false_when_target_is_not_an_identifier()
    {
        var node = FirstNode<LockStatementSyntax>("class C { object o = new object(); void M() { lock (this.o) { } } }");
        Assert.False(StmtFacts.LockOnLocal(node));
    }

    [Fact]
    public void LockOnLocal_is_false_when_not_inside_a_method()
    {
        var node = FirstNode<LockStatementSyntax>("class C { object o = new object(); C() { lock (o) { } } }");
        Assert.False(StmtFacts.LockOnLocal(node));
    }

    [Fact]
    public void LockOnLocal_is_false_when_target_is_a_parameter_not_a_local()
    {
        var node = FirstNode<LockStatementSyntax>(
            "class C { void M(object obj) { int other = 1; lock (obj) { } } }");
        Assert.False(StmtFacts.LockOnLocal(node));
    }

    [Fact]
    public void LockOnLocal_is_true_when_target_is_a_local_variable()
    {
        var node = FirstNode<LockStatementSyntax>(
            "class C { void M() { object obj = new object(); lock (obj) { } } }");
        Assert.True(StmtFacts.LockOnLocal(node));
    }

    [Fact]
    public void InfiniteWhile_is_false_for_non_while_node()
    {
        Assert.False(StmtFacts.InfiniteWhile(Expression("1")));
    }

    [Fact]
    public void InfiniteWhile_is_false_when_condition_is_not_literal_true()
    {
        var node = FirstNode<WhileStatementSyntax>("class C { void M(bool b) { while (b) { } } }");
        Assert.False(StmtFacts.InfiniteWhile(node));
    }

    [Fact]
    public void UnreachableIf_is_false_for_non_if_node()
    {
        Assert.False(StmtFacts.UnreachableIf(Expression("1")));
    }

    [Fact]
    public void MissingSwitchDefault_is_false_for_non_switch_node()
    {
        Assert.False(StmtFacts.MissingSwitchDefault(Expression("1")));
    }

    [Fact]
    public void FewSwitchCases_is_false_for_non_switch_node()
    {
        Assert.False(StmtFacts.FewSwitchCases(Expression("1")));
    }

    private static ExpressionSyntax Expression(string expression)
    {
        var source = $"class C {{ void M() {{ var _ = {expression}; }} }}";
        var tree = CSharpSyntaxTree.ParseText(source);
        return tree.GetRoot().DescendantNodes().OfType<EqualsValueClauseSyntax>().First().Value;
    }

    private static T FirstNode<T>(string source)
        where T : SyntaxNode =>
        CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().OfType<T>().First();
}

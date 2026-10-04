using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Tests;

public class CatchFactsTests
{
    [Fact]
    public void CatchType_is_empty_for_non_catch_node()
    {
        Assert.Equal(string.Empty, CatchFacts.CatchType(Expression("1")));
    }

    [Fact]
    public void CatchType_is_empty_for_catch_without_declared_type()
    {
        var node = FirstNode<CatchClauseSyntax>("class C { void M() { try { } catch { } } }");
        Assert.Equal(string.Empty, CatchFacts.CatchType(node));
    }

    [Fact]
    public void CatchType_returns_declared_type_name()
    {
        var node = FirstNode<CatchClauseSyntax>("class C { void M() { try { } catch (System.Exception) { } } }");
        Assert.Equal("Exception", CatchFacts.CatchType(node));
    }

    [Fact]
    public void ThrownType_is_empty_for_non_throw_node()
    {
        Assert.Equal(string.Empty, CatchFacts.ThrownType(Expression("1")));
    }

    [Fact]
    public void ThrownType_is_empty_for_bare_throw()
    {
        var node = FirstNode<ThrowStatementSyntax>("class C { void M() { try { } catch { throw; } } }");
        Assert.Equal(string.Empty, CatchFacts.ThrownType(node));
    }

    [Fact]
    public void ThrownType_returns_identifier_name_when_rethrowing_a_variable()
    {
        var node = FirstNode<ThrowStatementSyntax>(
            "class C { void M() { try { } catch (System.Exception ex) { throw ex; } } }");
        Assert.Equal("ex", CatchFacts.ThrownType(node));
    }

    [Fact]
    public void ThrownType_returns_created_type_name()
    {
        var node = FirstNode<ThrowStatementSyntax>("class C { void M() { throw new System.Exception(); } }");
        Assert.Equal("Exception", CatchFacts.ThrownType(node));
    }

    [Fact]
    public void Rethrows_is_false_for_non_catch_node()
    {
        Assert.False(CatchFacts.Rethrows(Expression("1")));
    }

    [Fact]
    public void Rethrows_is_false_when_block_has_more_than_one_statement()
    {
        var node = FirstNode<CatchClauseSyntax>(
            "class C { void M() { try { } catch { System.Console.WriteLine(); throw; } } }");
        Assert.False(CatchFacts.Rethrows(node));
    }

    [Fact]
    public void Rethrows_is_true_for_single_throw_statement()
    {
        var node = FirstNode<CatchClauseSyntax>(
            "class C { void M() { try { } catch (System.Exception ex) { throw ex; } } }");
        Assert.True(CatchFacts.Rethrows(node));
    }

    [Fact]
    public void Rethrows_is_true_when_the_sole_statement_throws_a_new_exception()
    {
        var node = FirstNode<CatchClauseSyntax>(
            "class C { void M() { try { } catch (System.IO.IOException) { throw new System.Exception(\"failed\"); } } }");
        Assert.True(CatchFacts.Rethrows(node));
    }

    [Fact]
    public void Rethrows_is_false_for_a_bare_throw()
    {
        var node = FirstNode<CatchClauseSyntax>(
            "class C { void M() { try { } catch (System.Exception) { throw; } } }");
        Assert.False(CatchFacts.Rethrows(node));
    }

    [Fact]
    public void BareRethrow_is_false_for_non_throw_node()
    {
        Assert.False(CatchFacts.BareRethrow(Expression("1")));
    }

    [Fact]
    public void BareRethrow_is_true_for_throw_of_identifier()
    {
        var node = FirstNode<ThrowStatementSyntax>(
            "class C { void M() { try { } catch (System.Exception ex) { throw ex; } } }");
        Assert.True(CatchFacts.BareRethrow(node));
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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp;
using Qlcheck.Languages.CSharp.Catalog;
using Qlcheck.Languages.CSharp.Catalog.Queries;

namespace Qlcheck.Tests;

public class SymbolsTests
{
    private const string AssemblyName = "symbols";

    private const string ListName = "System.Collections.Generic.List`1";

    [Fact]
    public void SymbolOf_resolves_the_referenced_symbol()
    {
        var ctx = CreateContext("class C { void M() { M(); } }");
        var invocation = ctx.Tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var symbol = Symbols.SymbolOf(ctx, invocation.Expression);
        Assert.NotNull(symbol);
        Assert.Equal("M", symbol.Name);
    }

    [Fact]
    public void Implements_is_false_for_null_type()
    {
        Assert.False(Symbols.Implements(null, "IDisposable"));
    }

    [Fact]
    public void Implements_is_true_when_type_itself_matches()
    {
        var compilation = CSharpCompilations.Create(AssemblyName, []);
        var type = compilation.GetTypeByMetadataName("System.IDisposable");
        Assert.True(Symbols.Implements(type, "IDisposable"));
    }

    [Fact]
    public void IsDisposable_is_false_for_non_creation_node()
    {
        var ctx = CreateContext("class C { void M() { var x = 1; } }");
        var node = ctx.Tree.GetRoot().DescendantNodes().OfType<EqualsValueClauseSyntax>().First().Value;
        Assert.False(Symbols.IsDisposable(ctx, node));
    }

    [Theory]
    [InlineData("System.IO.FileStream", "System.IO.FileStream")]
    [InlineData("System.IO.FileStream", "System.IO.Stream")]
    [InlineData("System.IO.FileStream", "System.IDisposable")]
    [InlineData(ListName, "System.Collections.Generic.IEnumerable`1")]
    [InlineData("System.Collections.Generic.Dictionary`2+KeyCollection", "System.Collections.Generic.Dictionary`2+KeyCollection")]
    public void Is_matches_the_type_its_bases_and_its_interfaces_by_qualified_name(string type, string qualifiedName)
    {
        var compilation = CSharpCompilations.Create(AssemblyName, []);

        Assert.True(Symbols.Is(compilation.GetTypeByMetadataName(type), qualifiedName));
    }

    [Fact]
    public void Is_matches_a_constructed_generic_by_its_definition()
    {
        var compilation = CSharpCompilations.Create(AssemblyName, []);
        var list = compilation.GetTypeByMetadataName(ListName)!;

        Assert.True(Symbols.Is(list.Construct(compilation.GetSpecialType(SpecialType.System_Int32)), ListName));
    }

    [Fact]
    public void Is_does_not_match_a_type_with_the_same_simple_name()
    {
        var ctx = CreateContext("namespace App { class DbCommand { } }");
        var type = (ITypeSymbol)Symbols.Declared(ctx, ctx.Tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single())!;

        Assert.False(Symbols.Is(type, "System.Data.Common.DbCommand"));
        Assert.True(Symbols.Is(type, "App.DbCommand"));
    }

    [Fact]
    public void Is_is_false_for_null_type()
    {
        Assert.False(Symbols.Is(null, "System.Object"));
    }

    [Theory]
    [InlineData("public class C { public int M() => 0; }", true)]
    [InlineData("internal class C { public int M() => 0; }", false)]
    [InlineData("class C { public int M() => 0; }", false)]
    [InlineData("public class O { private class C { public int M() => 0; } }", false)]
    [InlineData("public class C { protected int M() => 0; }", true)]
    [InlineData("public class C { protected internal int M() => 0; }", true)]
    [InlineData("public class C { private protected int M() => 0; }", false)]
    [InlineData("public interface I { int M(); }", true)]
    [InlineData("partial class C { public int M() => 0; } public partial class C { }", true)]
    public void A_member_is_visible_outside_the_assembly_only_when_it_and_its_types_are(string source, bool visible)
    {
        var ctx = CreateContext(source);
        var method = ctx.Tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        Assert.Equal(visible, Symbols.IsVisibleOutsideAssembly(ctx, method));
    }

    [Fact]
    public void A_field_declaration_resolves_through_its_variables()
    {
        var ctx = CreateContext("public class C { public int A, B; } internal class D { public int A, B; }");
        var fields = ctx.Tree.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>().ToList();
        Assert.True(Symbols.IsVisibleOutsideAssembly(ctx, fields[0]));
        Assert.False(Symbols.IsVisibleOutsideAssembly(ctx, fields[1]));
    }

    private static WalkContext CreateContext(string source)
    {
        var file = new SourceFile("Repo.cs", source);
        var tree = CSharpTrees.Parse(file);
        var compilation = CSharpCompilations.Create(AssemblyName, [tree]);
        var model = compilation.GetSemanticModel(tree);
        return WalkContext.Create(file, tree, model, new Dictionary<string, string>());
    }
}

using Microsoft.CodeAnalysis;
using Qlcheck.Languages.CSharp.Catalog.Dataflow;

namespace Qlcheck.Tests;

public class SinkTests
{
    private const string Process = "System.Diagnostics.Process";

    private const string StartInfo = "System.Diagnostics.ProcessStartInfo";

    private const string FileName = "fileName";

    private const string CommandText = "CommandText";

    private const string StartMethod = "Start";

    private const string PathParameter = "path";

    private const string AssemblyName = "sinks";

    private static readonly Compilation Platform = CSharpCompilations.Create(AssemblyName, []);

    [Fact]
    public void Call_matches_the_method_by_type_name_and_parameter_name()
    {
        var start = Method(Process, StartMethod, FileName);

        Assert.True(Sink.Call(Process, StartMethod, FileName).Matches(start, FileName));
        Assert.False(Sink.Call(Process, StartMethod, "arguments").Matches(start, FileName));
        Assert.False(Sink.Call(Process, "Kill", FileName).Matches(start, FileName));
    }

    [Theory]
    [InlineData("ReadAllText", true)]
    [InlineData("WriteAllText", true)]
    [InlineData("Delete", true)]
    [InlineData("Exists", false)]
    public void Call_member_takes_alternatives_and_a_trailing_wildcard(string method, bool matches)
    {
        var sink = Sink.Call("System.IO.File", "Read*|Write*|Delete", PathParameter);

        Assert.Equal(matches, sink.Matches(Method("System.IO.File", method, PathParameter), PathParameter));
    }

    [Fact]
    public void Set_matches_a_property_through_an_interface_the_type_implements()
    {
        var property = Platform.GetTypeByMetadataName("System.Data.Common.DbCommand")!.GetMembers(CommandText).Single();

        Assert.True(Sink.Set("System.Data.IDbCommand", CommandText).Matches(property, null));
        Assert.False(Sink.Set("System.Data.IDbCommand", "Connection").Matches(property, null));
    }

    [Fact]
    public void Ctor_matches_a_constructor_parameter_and_not_a_method()
    {
        var ctor = Platform.GetTypeByMetadataName(StartInfo)!.Constructors.Single(method => method.Parameters.Length == 1);

        Assert.True(Sink.Ctor(StartInfo, FileName).Matches(ctor, FileName));
        Assert.False(Sink.Call(StartInfo, ".ctor", FileName).Matches(ctor, FileName));
        Assert.False(Sink.Ctor(StartInfo, FileName).Matches(Method(Process, StartMethod, FileName), FileName));
    }

    [Fact]
    public void A_type_with_the_same_simple_name_does_not_match()
    {
        var tree = CSharpTrees.Parse(new SourceFile("Repo.cs", "namespace App { class Process { public static void Start(string fileName) { } } }"));
        var start = CSharpCompilations.Create(AssemblyName, [tree]).GetTypeByMetadataName("App.Process")!.GetMembers(StartMethod).Single();

        Assert.False(Sink.Call(Process, StartMethod, FileName).Matches(start, FileName));
    }

    [Fact]
    public void Names_are_the_identifiers_a_body_must_contain_to_reach_the_sink()
    {
        Assert.Equal(["Read", "Write"], Sink.Call("System.IO.File", "Read*|Write", PathParameter).Names());
        Assert.Equal([CommandText], Sink.Set("System.Data.IDbCommand", CommandText).Names());
        Assert.Equal(["ProcessStartInfo"], Sink.Ctor(StartInfo, FileName).Names());
    }

    private static IMethodSymbol Method(string type, string name, string parameter) =>
        Platform.GetTypeByMetadataName(type)!
            .GetMembers(name)
            .OfType<IMethodSymbol>()
            .First(method => method.Parameters.Any(item => item.Name == parameter));
}

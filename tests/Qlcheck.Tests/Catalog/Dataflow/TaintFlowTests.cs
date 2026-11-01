using Qlcheck.Languages.CSharp.Catalog.Dataflow;

namespace Qlcheck.Tests;

public class TaintFlowTests
{
    private const string Header = "using System; using System.Data.Common; using System.Diagnostics;\n";

    private const string StartInfo = "System.Diagnostics.ProcessStartInfo";

    private const string FileName = "fileName";

    private const string UserId = "userId";

    private const string ConsoleRead = "Console.ReadLine()";

    private static readonly Sink CommandText = Sink.Set("System.Data.Common.DbCommand", nameof(System.Data.Common.DbCommand.CommandText));

    private static readonly Sink[] Sinks =
    [
        CommandText,
        Sink.Call("System.Diagnostics.Process", "Start", FileName),
        Sink.Ctor(StartInfo, FileName),
        Sink.Set(StartInfo, "FileName"),
        Sink.Call("System.Diagnostics.Process", "Start", "arguments").Shell(),
        Sink.Ctor(StartInfo, "arguments").Shell(),
        Sink.Set(StartInfo, "Arguments").Shell(),
        Sink.Call("System.IO.File", "ReadAllText", "path"),
        Sink.Ctor("System.Collections.Generic.List`1", "capacity"),
        Sink.Call("Microsoft.AspNetCore.Mvc.ControllerBase", "Redirect", "url"),
        Sink.ArraySize(),
        Sink.LoopBound(),
    ];

    private const string AspNetStubs = """
        namespace Microsoft.AspNetCore.Mvc
        {
            public interface IUrlHelper { bool IsLocalUrl(string url); }
            public abstract class ControllerBase { public IUrlHelper Url { get; set; } = null!; public object Redirect(string url) => url; public Microsoft.AspNetCore.Http.HttpRequest Request { get; } = null!; }
            public class FromQueryAttribute : System.Attribute { }
            public class ApiControllerAttribute : System.Attribute { }
            public class NonActionAttribute : System.Attribute { }
        }
        namespace Microsoft.AspNetCore.Http
        {
            public abstract class HttpRequest { public abstract System.Collections.Generic.IDictionary<string, string> Query { get; } public abstract string Path { get; } }
        }
        """;

    [Fact]
    public void A_public_parameter_reaching_a_sink_is_a_hit_naming_its_source()
    {
        var hit = Assert.Single(Run("public class R { public void M(DbCommand c, string userId) { c.CommandText = userId; } }"));

        Assert.Equal(CommandText, hit.Sink);
        Assert.Equal("c.CommandText = userId", hit.Node.ToString());
        Assert.Equal(TaintLabel.Untrusted, hit.Labels);
        Assert.Equal(new TaintSource(UserId, "parameter of public `M`", 2, PublicParameter: true), hit.Source);
        Assert.Empty(hit.Via);
    }

    [Theory]
    [InlineData("var query = \"SELECT \" + userId; c.CommandText = query;")]
    [InlineData("var query = $\"SELECT {userId}\"; c.CommandText = query;")]
    [InlineData("var query = \"SELECT 1\"; if (userId.Length > 0) { query = \"SELECT \" + userId; } c.CommandText = query;")]
    [InlineData("var query = \"SELECT 1\"; try { query = userId; } finally { c.CommandText = query; }")]
    public void Taint_follows_locals_concatenation_interpolation_and_branches(string body)
    {
        var hit = Assert.Single(Run($"public class R {{ public void M(DbCommand c, string userId) {{ {body} }} }}"));

        Assert.Equal(["query"], hit.Via);
    }

    [Theory]
    [InlineData("var query = \"SELECT \"; query += userId; c.CommandText = query;")]
    [InlineData("var query = \"SELECT \"; query += userId; query += \"'\"; c.CommandText = query;")]
    [InlineData("var query = (string?)null; query ??= userId; c.CommandText = query;")]
    public void Compound_assignment_adds_to_the_taint(string body)
    {
        var hit = Assert.Single(Run($"public class R {{ public void M(DbCommand c, string userId) {{ {body} }} }}"));

        Assert.Equal(["query"], hit.Via);
    }

    [Theory]
    [InlineData("var query = userId.Length > 3 ? \"SELECT \" + userId : \"SELECT 1\"; c.CommandText = query;")]
    [InlineData("var query = (userId.Length > 3 ? null : userId) ?? \"SELECT 1\"; c.CommandText = query;")]
    [InlineData("var query = userId.Length switch { 0 => \"SELECT 1\", _ => userId }; c.CommandText = query;")]
    [InlineData("var query = string.Format(\"SELECT {0}\", userId); c.CommandText = query;")]
    [InlineData("var query = string.Concat(\"SELECT \", userId); c.CommandText = query;")]
    [InlineData("var query = string.Join(\",\", new[] { \"SELECT \", userId }); c.CommandText = query;")]
    [InlineData("var query = userId.Trim().Substring(1).ToUpperInvariant(); c.CommandText = query;")]
    [InlineData("var query = new System.Text.StringBuilder(userId).ToString(); c.CommandText = query;")]
    [InlineData("var builder = new System.Text.StringBuilder(\"SELECT \"); builder.Append(userId).Append(\"'\"); var query = builder.ToString(); c.CommandText = query;")]
    [InlineData("var builder = new System.Text.StringBuilder(); builder.AppendFormat(\"SELECT {0}\", userId); var query = builder.ToString(); c.CommandText = query;")]
    [InlineData("var query = System.IO.Path.Combine(\"/data\", userId); c.CommandText = query;")]
    [InlineData("object boxed = userId; var query = boxed.ToString(); c.CommandText = query;")]
    public void Taint_follows_known_string_builder_and_path_operations(string body)
    {
        var hit = Assert.Single(Run($"public class R {{ public void M(DbCommand c, string userId) {{ {body} }} }}"));

        Assert.Contains("query", hit.Via);
    }

    [Theory]
    [InlineData("var list = new System.Collections.Generic.List<string>(); list.Add(userId); c.CommandText = list[0];")]
    [InlineData("var map = new System.Collections.Generic.Dictionary<string, string>(); map[\"k\"] = userId; c.CommandText = map[\"k\"];")]
    [InlineData("var list = new System.Collections.Generic.List<string>(); list.Add(userId); foreach (var item in list) { c.CommandText = item; }")]
    [InlineData("var parts = userId.Split(','); foreach (var part in parts) { c.CommandText = part; }")]
    [InlineData("var parts = userId.Split(','); c.CommandText = parts[0];")]
    [InlineData("var parts = userId.Split(','); c.CommandText = System.Linq.Enumerable.First(parts);")]
    [InlineData("var parts = userId.Split(','); c.CommandText = parts.Where(part => part.Length > 0).ToList()[0];")]
    [InlineData("var list = new System.Collections.Generic.List<string>(); foreach (var part in userId.Split(',')) { list.Add(part); } c.CommandText = string.Join(\",\", list);")]
    public void Taint_follows_collections_loops_indexers_and_linq(string body)
    {
        Assert.Single(Run($"using System.Linq; public class R {{ public void M(DbCommand c, string userId) {{ {body} }} }}"));
    }

    [Theory]
    [InlineData("var list = new System.Collections.Generic.List<string>(); list.Add(\"SELECT 1\"); c.CommandText = list[0];")]
    [InlineData("var list = new System.Collections.Generic.List<string>(); list.Add(userId); c.CommandText = \"SELECT \" + list.Count;")]
    public void Clean_collections_and_counts_stay_clean(string body)
    {
        Assert.Empty(Run($"public class R {{ public void M(DbCommand c, string userId) {{ {body} }} }}"));
    }

    [Theory]
    [InlineData("var query = Helper(userId); c.CommandText = query;")]
    [InlineData("var query = userId.Contains('x') ? \"SELECT 1\" : \"SELECT 2\"; c.CommandText = query;")]
    [InlineData("var builder = new System.Text.StringBuilder(); builder.Append(userId); var other = new System.Text.StringBuilder(); c.CommandText = other.ToString();")]
    public void Unknown_calls_and_other_builders_stay_clean(string body)
    {
        Assert.Empty(Run($"public class R {{ public void M(DbCommand c, string userId) {{ {body} }} static string Helper(string s) => s; }}"));
    }

    [Fact]
    public void A_compound_set_of_a_sink_property_is_a_hit()
    {
        var hit = Assert.Single(Run("public class R { public void M(DbCommand c, string userId) { c.CommandText = \"SELECT \"; c.CommandText += userId; } }"));

        Assert.Equal("c.CommandText += userId", hit.Node.ToString());
    }

    [Theory]
    [InlineData("public void M(DbCommand c, string userId) { var q = userId; q = \"SELECT 1\"; c.CommandText = q; }")]
    [InlineData("public void M(DbCommand c, string userId) { c.CommandText = \"SELECT 1\"; }")]
    [InlineData("public void M(DbCommand c, Guid userId) { c.CommandText = \"SELECT \" + userId; }")]
    [InlineData("public void M(DbCommand c, int? userId) { c.CommandText = $\"SELECT {userId}\"; }")]
    [InlineData("public void M(DbCommand c, Enum userId) { c.CommandText = \"SELECT \" + userId; }")]
    [InlineData("private void M(DbCommand c, string userId) { c.CommandText = userId; }")]
    [InlineData("internal void M(DbCommand c, string userId) { c.CommandText = userId; }")]
    public void Clean_values_kills_safe_types_and_non_public_methods_give_nothing(string member)
    {
        Assert.Empty(Run($"public class R {{ {member} }}"));
    }

    [Theory]
    [InlineData("_query = \"SELECT \" + userId; c.CommandText = _query;")]
    [InlineData("this._query = \"SELECT \" + userId; c.CommandText = this._query;")]
    [InlineData("Shared = userId; c.CommandText = Shared;")]
    public void A_field_written_and_read_in_one_member_carries_taint(string body)
    {
        Assert.Single(Run($"public class R {{ string _query = \"\"; static string Shared = \"\"; public void M(DbCommand c, string userId) {{ {body} }} }}"));
    }

    [Theory]
    [InlineData("public void Set(string userId) { _query = userId; } public void Use(DbCommand c) { c.CommandText = _query; }")]
    [InlineData("public void M(DbCommand c, R other, string userId) { other._query = userId; c.CommandText = _query; }")]
    [InlineData("public void M(DbCommand c, string userId) { _query = userId; _query = \"SELECT 1\"; c.CommandText = _query; }")]
    public void Fields_across_members_or_objects_and_overwritten_fields_are_clean(string members)
    {
        Assert.Empty(Run($"public class R {{ string _query = \"\"; {members} }}"));
    }

    [Theory]
    [InlineData("Action run = () => { c.CommandText = \"SELECT \" + userId; }; run();")]
    [InlineData("var query = \"SELECT \" + userId; Action run = () => c.CommandText = query; run();")]
    [InlineData("Run(\"SELECT \" + userId); void Run(string sql) { c.CommandText = sql; }")]
    [InlineData("var query = \"SELECT \" + userId; Run(); void Run() { c.CommandText = query; }")]
    [InlineData("Outer(userId); void Outer(string a) { Inner(a); } void Inner(string b) { c.CommandText = b; }")]
    [InlineData("Action outer = () => { Action inner = () => c.CommandText = userId; inner(); }; outer();")]
    public void Lambdas_and_local_functions_see_captured_and_passed_taint(string body)
    {
        Assert.Single(Run($"public class R {{ public void M(DbCommand c, string userId) {{ {body} }} }}"));
    }

    [Theory]
    [InlineData("Run(\"SELECT 1\"); void Run(string sql) { c.CommandText = sql; }")]
    [InlineData("Action<string> run = sql => c.CommandText = sql; run(\"SELECT 1\");")]
    public void Lambda_and_local_function_parameters_are_not_sources(string body)
    {
        Assert.Empty(Run($"public class R {{ public void M(DbCommand c, string userId) {{ {body} }} }}"));
    }

    [Theory]
    [InlineData("var n = int.Parse(userId);", "Numeric")]
    [InlineData("int.TryParse(userId, out var n);", "Numeric")]
    [InlineData("var n = Convert.ToInt32(userId);", "Numeric")]
    [InlineData("var n = (int)decimal.Parse(userId);", "Numeric")]
    [InlineData("var n = Math.Max(int.Parse(userId), 1);", "Numeric")]
    [InlineData("var n = Math.Min(int.Parse(userId), int.Parse(userId));", "Numeric")]
    [InlineData("var n = Math.Min(int.Parse(userId), 100);", "None")]
    [InlineData("var n = Math.Clamp(int.Parse(userId), 0, 100);", "None")]
    [InlineData("var n = int.Parse(userId); if (n > 100) { return; }", "None")]
    [InlineData("var n = int.Parse(userId); if (100 < n) { throw new ArgumentException(); }", "None")]
    [InlineData("var n = int.Parse(userId); if (n < 0) { return; }", "Numeric")]
    public void Parsing_makes_numbers_and_a_constant_upper_bound_clears_them(string body, string label)
    {
        var hits = Run($"public class R {{ public void M(string userId) {{ {body} var list = new System.Collections.Generic.List<int>(n); }} }}");

        Assert.Equal(label, hits.Count == 0 ? "None" : hits.Single().Labels.ToString());
    }

    [Theory]
    [InlineData("var g = Guid.Parse(userId); System.IO.File.ReadAllText(g.ToString());")]
    [InlineData("var d = DateTime.Parse(userId); System.IO.File.ReadAllText(d.ToString());")]
    [InlineData("var k = Enum.Parse<DayOfWeek>(userId); System.IO.File.ReadAllText(k.ToString());")]
    public void Parsing_into_a_safe_non_numeric_type_clears_taint(string body)
    {
        Assert.Empty(Run($"public class R {{ public void M(string userId) {{ {body} }} }}"));
    }

    [Theory]
    [InlineData("var name = System.IO.Path.GetFileName(userId); System.IO.File.ReadAllText(name);", "FileName")]
    [InlineData("var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(\"/data\", userId)); if (!full.StartsWith(\"/data/\")) { return; } System.IO.File.ReadAllText(full);", "ContainedPath")]
    [InlineData("var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(\"/data\", userId)); System.IO.File.ReadAllText(full);", "Untrusted")]
    [InlineData("var path = System.IO.Path.Combine(\"/data\", userId); if (!path.StartsWith(\"/data/\")) { return; } System.IO.File.ReadAllText(path);", "Untrusted")]
    public void File_names_and_contained_paths_are_labelled_for_the_path_rows(string body, string label)
    {
        var hit = Assert.Single(Run($"public class R {{ public void M(string userId) {{ {body} }} }}"));

        Assert.Equal(label, hit.Labels.ToString());
    }

    [Theory]
    [InlineData("public class R : Microsoft.AspNetCore.Mvc.ControllerBase { public object Go(string returnUrl) => Redirect(returnUrl); }", "parameter of action `Go`")]
    [InlineData("[Microsoft.AspNetCore.Mvc.ApiController] public class R { public object Go(string returnUrl, Microsoft.AspNetCore.Mvc.ControllerBase c) => c.Redirect(returnUrl); }", "parameter of action `Go`")]
    [InlineData("class R { object Go([Microsoft.AspNetCore.Mvc.FromQuery] string returnUrl, Microsoft.AspNetCore.Mvc.ControllerBase c) => c.Redirect(returnUrl); }", "`[FromQuery]` parameter of `Go`")]
    [InlineData("public class R : Microsoft.AspNetCore.Mvc.ControllerBase { public object Go() => Redirect(Request.Query[\"next\"]); }", "request input")]
    [InlineData("public class R : Microsoft.AspNetCore.Mvc.ControllerBase { public object Go() => Redirect(Request.Path); }", "request input")]
    public void Asp_net_requests_are_entry_points(string source, string description)
    {
        var hit = Assert.Single(Run($"{AspNetStubs} {source}"));

        Assert.False(hit.Source.PublicParameter);
        Assert.Equal(description, hit.Source.Description);
    }

    [Theory]
    [InlineData("int size", 1)]
    [InlineData("[System.ComponentModel.DataAnnotations.Range(1, 4096)] int size", 0)]
    public void A_range_attribute_bounds_a_numeric_action_parameter(string parameter, int hits)
    {
        Assert.Equal(hits, Run($"{AspNetStubs} public class R : Microsoft.AspNetCore.Mvc.ControllerBase {{ public byte[] Go({parameter}) => new byte[size]; }}").Count);
    }

    [Theory]
    [InlineData("public class R : Microsoft.AspNetCore.Mvc.ControllerBase { [Microsoft.AspNetCore.Mvc.NonAction] public object Go(string returnUrl) => Redirect(returnUrl); }")]
    [InlineData("public class R : Microsoft.AspNetCore.Mvc.ControllerBase { protected object Go(string returnUrl) => Redirect(returnUrl); }")]
    public void Non_actions_are_not_entry_points(string source)
    {
        Assert.True(Assert.Single(Run($"{AspNetStubs} {source}")).Source.PublicParameter);
    }

    [Theory]
    [InlineData("if (!Url.IsLocalUrl(returnUrl)) { return null!; } return Redirect(returnUrl);", 0)]
    [InlineData("if (Url.IsLocalUrl(returnUrl)) { return Redirect(returnUrl); } return null!;", 0)]
    [InlineData("return Redirect(returnUrl);", 1)]
    public void A_local_url_check_cleans_the_redirect_target(string body, int hits)
    {
        Assert.Equal(hits, Run($"{AspNetStubs} public class R : Microsoft.AspNetCore.Mvc.ControllerBase {{ public object Go(string returnUrl) {{ {body} }} }}").Count);
    }

    [Theory]
    [InlineData("public class R { protected void M(DbCommand c, string userId) { c.CommandText = userId; } }", 1)]
    [InlineData("public class Outer { public class R { public void M(DbCommand c, string userId) { c.CommandText = userId; } } }", 1)]
    [InlineData("class R { public void M(DbCommand c, string userId) { c.CommandText = userId; } }", 0)]
    [InlineData("public class Outer { class R { public void M(DbCommand c, string userId) { c.CommandText = userId; } } }", 0)]
    public void Only_methods_visible_outside_the_assembly_take_untrusted_parameters(string source, int hits)
    {
        Assert.Equal(hits, Run(source).Count);
    }

    [Fact]
    public void An_entry_point_outranks_a_public_parameter_at_a_join()
    {
        var hit = Assert.Single(Run("public class R { public void M(DbCommand c, string userId) { var q = userId; if (userId.Length > 1) { q = Console.ReadLine(); } c.CommandText = q; } }"));

        Assert.False(hit.Source.PublicParameter);
        Assert.Equal(ConsoleRead, hit.Source.Name);
    }

    [Theory]
    [InlineData("Fact")]
    [InlineData("Theory")]
    [InlineData("TestMethod")]
    public void Test_method_parameters_are_not_sources(string attribute)
    {
        Assert.Empty(Run($"public class {attribute}Attribute : Attribute {{ }} public class T {{ [{attribute}] public void M(DbCommand c, string userId) {{ c.CommandText = userId; }} }}"));
    }

    [Theory]
    [InlineData("var buffer = new byte[n * 2];", "new byte[n * 2]")]
    [InlineData("var grid = new int[4, n];", "new int[4, n]")]
    [InlineData("for (var i = 0; i < n; i++) { }", "i < n")]
    [InlineData("var i = 0; while (n >= i) { i++; }", "n >= i")]
    [InlineData("var i = 0; do { i++; } while (i <= n + 1);", "i <= n + 1")]
    public void Array_sizes_and_loop_bounds_are_structural_sinks(string body, string at)
    {
        var hit = Assert.Single(Run($"public class R {{ public void M() {{ var n = int.Parse(Console.ReadLine() ?? \"0\"); {body} }} }}"));

        Assert.Equal(at, hit.Node.ToString());
        Assert.Equal(TaintLabel.Numeric, hit.Labels);
    }

    [Theory]
    [InlineData("var buffer = new byte[16]; var list = new[] { n };")]
    [InlineData("for (var i = 0; i < 10; i++) { }")]
    [InlineData("for (var i = 0; i < 10 && i != n; i++) { }")]
    public void Constant_sizes_and_bounds_are_not_sinks(string body)
    {
        Assert.Empty(Run($"public class R {{ public void M() {{ var n = int.Parse(Console.ReadLine() ?? \"0\"); {body} }} }}"));
    }

    [Fact]
    public void A_body_with_a_loop_but_no_entry_point_is_not_built_for_structural_sinks()
    {
        Assert.Empty(Run("public class R { public void M(int n) { for (var i = 0; i < n; i++) { } var buffer = new byte[n]; } }"));
    }

    [Fact]
    public void A_numeric_parameter_carries_only_the_numeric_label_even_as_text()
    {
        var hits = Run("public class R { public void M(DbCommand c, int n) { c.CommandText = \"SELECT \" + n.ToString(); c.CommandText = (string)(object)n; } }");

        Assert.Equal([TaintLabel.Numeric, TaintLabel.Numeric], hits.Select(hit => hit.Labels));
    }

    [Theory]
    [InlineData("var query = await System.Threading.Tasks.Task.FromResult(userId); c.CommandText = query;")]
    [InlineData("var query = await System.Threading.Tasks.Task.FromResult(userId).ConfigureAwait(false); c.CommandText = query;")]
    [InlineData("var pending = System.Threading.Tasks.Task.FromResult(userId); var query = await pending; c.CommandText = query;")]
    public void Await_gives_the_taint_of_the_awaited_task(string body)
    {
        Assert.Single(Run($"public class R {{ public async System.Threading.Tasks.Task M(DbCommand c, string userId) {{ {body} }} }}"));
    }

    [Theory]
    [InlineData("var s = Console.In.ReadLine();", "Console.In.ReadLine()")]
    [InlineData("var s = await Console.In.ReadLineAsync();", "Console.In.ReadLineAsync()")]
    public void Console_in_is_a_source(string read, string name)
    {
        var hit = Assert.Single(Run($"public class R {{ async System.Threading.Tasks.Task M(DbCommand c) {{ {read} c.CommandText = s; }} }}"));

        Assert.Equal(name, hit.Source.Name);
    }

    [Theory]
    [InlineData("catch (Exception ex) { System.IO.File.ReadAllText(ex.StackTrace!); }", "ex.StackTrace", "ExceptionDetail")]
    [InlineData("catch (Exception ex) { System.IO.File.ReadAllText(ex.ToString()); }", "ex.ToString()", "ExceptionDetail")]
    [InlineData("catch (Exception ex) { System.IO.File.ReadAllText(ex.Message); }", null, null)]
    public void Exception_details_are_a_row_scoped_source(string handler, string? name, string? label)
    {
        var hits = Run($"public class R {{ void M() {{ try {{ }} {handler} }} }}");

        Assert.Equal(name, hits.SingleOrDefault()?.Source.Name);
        Assert.Equal(label, hits.SingleOrDefault()?.Labels.ToString());
    }

    [Fact]
    public void An_archive_entry_name_is_a_row_scoped_source()
    {
        var hit = Assert.Single(Run("public class R { void M(System.IO.Compression.ZipArchive zip) { foreach (var entry in zip.Entries) { System.IO.File.ReadAllText(entry.FullName); } } }"));

        Assert.Equal(TaintLabel.ArchiveEntry, hit.Labels);
        Assert.Equal("entry.FullName", hit.Source.Name);
    }

    [Theory]
    [InlineData("System.Net.WebUtility.HtmlEncode(userId)")]
    [InlineData("System.Net.WebUtility.UrlEncode(userId)")]
    [InlineData("Uri.EscapeDataString(userId)")]
    [InlineData("System.Web.HttpUtility.HtmlEncode(userId)")]
    public void Encoders_clear_the_taint(string encoded)
    {
        Assert.Empty(Run($"public class R {{ public void M(DbCommand c, string userId) {{ c.CommandText = {encoded}; }} }}"));
    }

    [Fact]
    public void Console_input_is_a_source()
    {
        var hit = Assert.Single(Run("public class R { void M(DbCommand c) { var s = Console.ReadLine(); c.CommandText = \"SELECT \" + s; } }"));

        Assert.Equal(ConsoleRead, hit.Source.Name);
        Assert.Equal(TaintLabel.Untrusted, hit.Labels);
    }

    [Theory]
    [InlineData("Process.Start(file);", "Process.Start(file)")]
    [InlineData("var psi = new ProcessStartInfo(file);", "new ProcessStartInfo(file)")]
    [InlineData("var psi = new ProcessStartInfo { FileName = file, UseShellExecute = true };", "FileName = file")]
    [InlineData("var p = new Process(); p.StartInfo.FileName = file;", "p.StartInfo.FileName = file")]
    public void Calls_constructors_initializers_and_nested_receivers_are_sinks(string body, string at)
    {
        var hit = Assert.Single(Run($"public class R {{ public void M(string file) {{ {body} }} }}"));

        Assert.Equal(at, hit.Node.ToString());
    }

    [Theory]
    [InlineData("Process.Start(\"sh\", \"-c \" + args);", "Process.Start(\"sh\", \"-c \" + args)")]
    [InlineData("var psi = new ProcessStartInfo(\"/bin/bash\", args);", "new ProcessStartInfo(\"/bin/bash\", args)")]
    [InlineData("var psi = new ProcessStartInfo { FileName = \"cmd.exe\", Arguments = \"/c \" + args };", "Arguments = \"/c \" + args")]
    [InlineData("var p = new Process(); p.StartInfo.FileName = \"sh\"; p.StartInfo.Arguments = \"-c \" + args;", "p.StartInfo.Arguments = \"-c \" + args")]
    public void Arguments_are_a_sink_when_the_program_is_a_constant_shell(string body, string at)
    {
        var hit = Assert.Single(Run($"public class R {{ public void M(string args) {{ {body} }} }}"));

        Assert.Equal(at, hit.Node.ToString());
    }

    [Theory]
    [InlineData("Process.Start(\"git\", args);")]
    [InlineData("var psi = new ProcessStartInfo { FileName = \"git\", Arguments = args };")]
    [InlineData("var p = new Process(); var q = new Process(); q.StartInfo.FileName = \"sh\"; p.StartInfo.Arguments = args;")]
    public void Arguments_of_another_program_or_object_are_not_a_shell_sink(string body)
    {
        Assert.Empty(Run($"public class R {{ public void M(string args) {{ {body} }} }}"));
    }

    [Theory]
    [InlineData("if (file != \"ls\" && file != \"pwd\") { return; } Process.Start(file);")]
    [InlineData("if (file == \"ls\") { Process.Start(file); }")]
    [InlineData("if (!(\"ls\" != file)) { Process.Start(file); }")]
    [InlineData("if (!Tools.Contains(file)) { throw new ArgumentException(nameof(file)); } Process.Start(file);")]
    [InlineData("if (Tools.Contains(file)) { Process.Start(file); }")]
    public void A_comparison_with_a_constant_or_a_read_only_set_cleans_the_guarded_path(string body)
    {
        Assert.Empty(Run($"public class R {{ static readonly System.Collections.Generic.HashSet<string> Tools = new() {{ \"ls\" }}; public void M(string file) {{ {body} }} }}"));
    }

    [Theory]
    [InlineData("if (file == \"ls\") { return; } Process.Start(file);")]
    [InlineData("if (file != \"ls\") { Process.Start(file); }")]
    [InlineData("if (!Tools.Contains(file)) { Process.Start(file); }")]
    [InlineData("if (Mutable.Contains(file)) { Process.Start(file); }")]
    [InlineData("if (file == Other) { Process.Start(file); }")]
    public void A_guard_cleans_only_the_path_where_it_holds(string body)
    {
        Assert.Single(Run($"public class R {{ static readonly System.Collections.Generic.HashSet<string> Tools = new() {{ \"ls\" }}; static System.Collections.Generic.HashSet<string> Mutable = new(); static string Other = \"ls\"; public void M(string file) {{ {body} }} }}"));
    }

    [Fact]
    public void A_type_that_only_shares_the_sink_name_is_not_a_sink()
    {
        Assert.Empty(Run("public class Process { public static void Start(string fileName) { } } public class R { public void M(string file) { Process.Start(file); } }"));
    }

    private static IReadOnlyList<SinkHit> Run(string source)
    {
        var tree = CSharpTrees.Parse(new SourceFile("Repo.cs", Header + source));
        var model = CSharpCompilations.Create("taint", [tree]).GetSemanticModel(tree);
        var errors = model.GetDiagnostics().Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        Assert.Empty(errors);
        var filter = new SinkFilter(Sinks);
        return new BodyGraphs(tree, model).Where((body, tokens) => filter.Relevant(model, body, tokens)).SelectMany(body => TaintFlow.Run(body, Sinks)).ToList();
    }
}

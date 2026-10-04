using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class PatternsTests
{
    [Fact]
    public void SuffixFlags_reports_enum_ending_in_flags()
    {
        var ctx = MatchFixtures.Context("enum ColorFlags { None }");
        Patterns.SuffixFlags(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void SuffixEnum_reports_enum_ending_in_enum()
    {
        var ctx = MatchFixtures.Context("enum ColorEnum { None }");
        Patterns.SuffixEnum(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void NullDeref_ignores_braceless_if_body()
    {
        var ctx = MatchFixtures.Context("class C { void M(object x) { if (x == null) M(x); } }");
        Patterns.NullDeref(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NullDeref_ignores_condition_that_is_not_an_equality_check()
    {
        var ctx = MatchFixtures.Context("class C { void M(bool b) { if (b) { } } }");
        Patterns.NullDeref(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NullDeref_ignores_comparison_to_a_non_null_literal()
    {
        var ctx = MatchFixtures.Context("class C { void M(int x) { if (x == 5) { } } }");
        Patterns.NullDeref(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void LoopBound_ignores_for_loop_without_a_condition()
    {
        var ctx = MatchFixtures.Context("class C { void M() { for (var i = 0; ; i++) { } } }");
        Patterns.LoopBound(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void LoopBound_ignores_an_internal_method()
    {
        var ctx = MatchFixtures.Context(
            "class C { internal void M(int maxBatch) { for (var i = 0; i < maxBatch; i++) { } } }");
        Patterns.LoopBound(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void LoopBound_reports_a_public_method_bound_by_a_parameter()
    {
        var ctx = MatchFixtures.Context(
            "class C { public void M(int maxBatch) { for (var i = 0; i < maxBatch; i++) { } } }");
        Patterns.LoopBound(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void AllocDos_ignores_array_creation_with_no_parameter_derived_size()
    {
        var ctx = MatchFixtures.Context("class C { void M() { var local = 5; var x = new int[local]; } }");
        Patterns.AllocDos(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void AllocDos_ignores_identifier_outside_a_method()
    {
        var ctx = MatchFixtures.Context("class C { C(int x) { var arr = new int[x]; } }");
        Patterns.AllocDos(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ConstantReturn_ignores_expression_bodied_method()
    {
        var ctx = MatchFixtures.Context("class C { int M() => 1; }");
        Patterns.ConstantReturn(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ConstantReturn_ignores_method_returning_different_literals()
    {
        var ctx = MatchFixtures.Context("class C { int M(bool b) { if (b) { return 1; } return 2; } }");
        Patterns.ConstantReturn(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ElseRequired_ignores_if_without_an_else()
    {
        var ctx = MatchFixtures.Context("class C { void M(bool b) { if (b) { } } }");
        Patterns.ElseRequired(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void IdenticalBranch_ignores_if_without_an_else()
    {
        var ctx = MatchFixtures.Context("class C { void M(bool b) { if (b) { } } }");
        Patterns.IdenticalBranch(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void OneStatement_ignores_lines_matching_for_get_and_set_accessors()
    {
        var ctx = MatchFixtures.Context(
            """
            class C
            {
                int P
                {
                    get;
                    set;
                }

                void M()
                {
                    for (var i = 0; i < 10; i++) { }
                }
            }
            """);
        Patterns.OneStatement(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void OneStatement_ignores_semicolons_inside_string_literals()
    {
        var ctx = MatchFixtures.Context(
            """
            class C
            {
                const string Sql = "LOCK TABLE users IN EXCLUSIVE MODE;";
                const string ConnectionString = "Host=x;Database=y;";
                const string ContentType = "text/csv; charset=utf-8";
            }
            """);
        Patterns.OneStatement(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void OneStatement_ignores_a_for_loop_with_its_body_on_the_same_line()
    {
        var ctx = MatchFixtures.Context(
            """
            class C
            {
                void M()
                {
                    for (var i = 0; i < 10; i++) DoWork();
                }

                void DoWork() { }
            }
            """);
        Patterns.OneStatement(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void OneStatement_ignores_get_and_set_accessors_on_the_same_line()
    {
        var ctx = MatchFixtures.Context("class C { int P { get; set; } }");
        Patterns.OneStatement(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void OneStatement_reports_two_real_statements_on_one_line()
    {
        var ctx = MatchFixtures.Context(
            """
            class C
            {
                void M()
                {
                    return; return;
                }
            }
            """);
        Patterns.OneStatement(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void DateTimeMissingKind_reports_construction_with_no_kind_argument()
    {
        var ctx = MatchFixtures.Context("class C { DateTime M() => new DateTime(2024, 1, 1); }");
        Patterns.DateTimeMissingKind(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void DateTimeMissingKind_ignores_construction_with_a_kind_argument()
    {
        var ctx = MatchFixtures.Context(
            "class C { DateTime M() => new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc); }");
        Patterns.DateTimeMissingKind(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void DateTimeMissingKind_ignores_a_call_to_specify_kind()
    {
        var ctx = MatchFixtures.Context(
            "class C { DateTime M(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc); }");
        Patterns.DateTimeMissingKind(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void EmptyClass_reports_a_class_with_no_members()
    {
        var ctx = MatchFixtures.Context("class C { }");
        Patterns.EmptyClass(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void EmptyClass_ignores_a_marker_exception_type()
    {
        var ctx = MatchFixtures.Context("class MyMarkerException : Exception { }");
        Patterns.EmptyClass(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void EmptyClass_ignores_an_exception_type_deeper_in_the_hierarchy()
    {
        var ctx = MatchFixtures.Context(
            """
            class AppException : Exception
            {
                public AppException(string message) : base(message) { }
            }

            class SpecificException : AppException
            {
            }
            """);
        Patterns.EmptyClass(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void SetMinMax_reports_min_on_a_hash_set()
    {
        var ctx = MatchFixtures.Context(
            "using System.Collections.Generic; class C { int M() { var s = new HashSet<int>(); return s.Min(); } }");
        SymbolPatterns.SetMinMax(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void SetMinMax_reports_max_on_a_sorted_set()
    {
        var ctx = MatchFixtures.Context(
            "using System.Collections.Generic; class C { int M() { var s = new SortedSet<int>(); return s.Max(); } }");
        SymbolPatterns.SetMinMax(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void SetMinMax_ignores_math_min()
    {
        var ctx = MatchFixtures.Context("class C { int M(int a, int b) => System.Math.Min(a, b); }");
        SymbolPatterns.SetMinMax(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void SetMinMax_ignores_int_min()
    {
        var ctx = MatchFixtures.Context("class C { int M(int a, int b) => int.Min(a, b); }");
        SymbolPatterns.SetMinMax(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void SetMinMax_ignores_min_on_a_list()
    {
        var ctx = MatchFixtures.Context(
            "using System.Collections.Generic; using System.Linq; "
            + "class C { int M() { var list = new List<int>(); return list.Min(); } }");
        SymbolPatterns.SetMinMax(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void JwtSigning_reports_signing_credentials_construction()
    {
        var ctx = MatchFixtures.Context(
            """
            class SigningCredentials { public SigningCredentials(object key, string algorithm) { } }
            class C { void M(object key) { var creds = new SigningCredentials(key, "HS256"); } }
            """);
        SymbolPatterns.JwtSigning(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void JwtSigning_reports_a_call_on_a_jwt_security_token_handler()
    {
        var ctx = MatchFixtures.Context(
            """
            class JwtSecurityTokenHandler { public string CreateToken(object descriptor) => string.Empty; }
            class C { void M(JwtSecurityTokenHandler handler, object descriptor) { handler.CreateToken(descriptor); } }
            """);
        SymbolPatterns.JwtSigning(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void JwtSigning_ignores_sign_on_an_unrelated_hmac_signer()
    {
        var ctx = MatchFixtures.Context(
            """
            class HmacSigner { public byte[] Sign(byte[] payload) => payload; }
            class C { void M(HmacSigner signer, byte[] payload) { signer.Sign(payload); } }
            """);
        SymbolPatterns.JwtSigning(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void AssertArgumentOrder_reports_a_literal_in_the_actual_position()
    {
        var ctx = MatchFixtures.Context(
            """
            class Assert { public static void AreEqual(object expected, object actual) { } }
            class C { void M(int actual) { Assert.AreEqual(actual, 5); } }
            """);
        SymbolPatterns.AssertArgumentOrder(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void AssertArgumentOrder_ignores_correct_order()
    {
        var ctx = MatchFixtures.Context(
            """
            class Assert { public static void AreEqual(object expected, object actual) { } }
            class C { void M(int actual) { Assert.AreEqual(5, actual); } }
            """);
        SymbolPatterns.AssertArgumentOrder(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void AssertArgumentOrder_ignores_an_unrelated_are_equal_method()
    {
        var ctx = MatchFixtures.Context(
            """
            class Comparer { public static void AreEqual(object a, object b) { } }
            class C { void M(int actual) { Comparer.AreEqual(actual, 5); } }
            """);
        SymbolPatterns.AssertArgumentOrder(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void AssertNoSideEffect_reports_a_call_inside_debug_assert()
    {
        var ctx = MatchFixtures.Context(
            """
            class Debug { public static void Assert(bool condition) { } }
            class C { bool Compute() => true; void M() { Debug.Assert(Compute()); } }
            """);
        SymbolPatterns.AssertNoSideEffect(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void AssertNoSideEffect_ignores_a_plain_field_condition()
    {
        var ctx = MatchFixtures.Context(
            """
            class Debug { public static void Assert(bool condition) { } }
            class C { bool flag; void M() { Debug.Assert(flag); } }
            """);
        SymbolPatterns.AssertNoSideEffect(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void AssertNoSideEffect_ignores_an_unrelated_assert_method()
    {
        var ctx = MatchFixtures.Context(
            """
            class Assert { public static void Assert(bool condition) { } }
            class C { bool Compute() => true; void M() { Assert.Assert(Compute()); } }
            """);
        SymbolPatterns.AssertNoSideEffect(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void CompleteAssertion_reports_debug_assert_with_no_message()
    {
        var ctx = MatchFixtures.Context(
            """
            class Debug { public static void Assert(bool condition) { } public static void Assert(bool condition, string message) { } }
            class C { bool flag; void M() { Debug.Assert(flag); } }
            """);
        SymbolPatterns.CompleteAssertion(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void CompleteAssertion_ignores_debug_assert_with_a_message()
    {
        var ctx = MatchFixtures.Context(
            """
            class Debug { public static void Assert(bool condition) { } public static void Assert(bool condition, string message) { } }
            class C { bool flag; void M() { Debug.Assert(flag, "must hold"); } }
            """);
        SymbolPatterns.CompleteAssertion(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoBooleanLiteralAssert_reports_a_literal_true_argument()
    {
        var ctx = MatchFixtures.Context(
            """
            class Assert { public static void True(bool condition) { } }
            class C { void M() { Assert.True(true); } }
            """);
        SymbolPatterns.NoBooleanLiteralAssert(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void NoBooleanLiteralAssert_ignores_a_variable_argument()
    {
        var ctx = MatchFixtures.Context(
            """
            class Assert { public static void True(bool condition) { } }
            class C { bool flag; void M() { Assert.True(flag); } }
            """);
        SymbolPatterns.NoBooleanLiteralAssert(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoBooleanLiteralAssert_ignores_an_unrelated_true_method()
    {
        var ctx = MatchFixtures.Context(
            """
            class Flag { public static void True(bool condition) { } }
            class C { void M() { Flag.True(true); } }
            """);
        SymbolPatterns.NoBooleanLiteralAssert(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    private const string QueryableIncludeExtension =
        """
        using System.Linq;
        static class Ext { public static IQueryable<T> Include<T>(this IQueryable<T> source, string path) => source; }
        """;

    [Fact]
    public void CartesianExplosion_reports_two_includes_on_a_queryable()
    {
        var ctx = MatchFixtures.Context(
            QueryableIncludeExtension + "\nclass C { void M(IQueryable<int> query) { var q = query.Include(\"A\").Include(\"B\"); } }");
        ResourcePatterns.CartesianExplosion(ctx, MatchFixtures.Id);
        Assert.NotEmpty(ctx.Findings);
    }

    [Fact]
    public void CartesianExplosion_ignores_a_single_include()
    {
        var ctx = MatchFixtures.Context(
            QueryableIncludeExtension + "\nclass C { void M(IQueryable<int> query) { var q = query.Include(\"A\"); } }");
        ResourcePatterns.CartesianExplosion(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void CartesianExplosion_ignores_include_on_a_non_queryable_type()
    {
        var ctx = MatchFixtures.Context(
            """
            class Builder { public Builder Include(string path) => this; }
            class C { void M(Builder b) { b.Include("A").Include("B"); } }
            """);
        ResourcePatterns.CartesianExplosion(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void DiscardedInclude_reports_an_include_used_as_a_statement()
    {
        var ctx = MatchFixtures.Context(
            QueryableIncludeExtension + "\nclass C { void M(IQueryable<int> query) { query.Include(\"A\"); } }");
        ResourcePatterns.DiscardedInclude(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void DiscardedInclude_ignores_a_reassigned_include()
    {
        var ctx = MatchFixtures.Context(
            QueryableIncludeExtension + "\nclass C { void M(IQueryable<int> query) { query = query.Include(\"A\"); } }");
        ResourcePatterns.DiscardedInclude(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void RedundantInclude_reports_a_duplicate_include_in_the_same_chain()
    {
        var ctx = MatchFixtures.Context(
            QueryableIncludeExtension + "\nclass C { void M(IQueryable<int> query) { var q = query.Include(\"A\").Include(\"A\"); } }");
        ResourcePatterns.RedundantInclude(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void RedundantInclude_ignores_distinct_includes()
    {
        var ctx = MatchFixtures.Context(
            QueryableIncludeExtension + "\nclass C { void M(IQueryable<int> query) { var q = query.Include(\"A\").Include(\"B\"); } }");
        ResourcePatterns.RedundantInclude(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void LockReadonlyField_reports_locking_on_a_mutable_field()
    {
        var ctx = MatchFixtures.Context(
            "class C { object _lock = new object(); void M() { System.Threading.Monitor.Enter(_lock); } }");
        ResourcePatterns.LockReadonlyField(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void LockReadonlyField_ignores_locking_on_a_readonly_field()
    {
        var ctx = MatchFixtures.Context(
            "class C { readonly object _lock = new object(); void M() { System.Threading.Monitor.Enter(_lock); } }");
        ResourcePatterns.LockReadonlyField(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void WeakLock_reports_locking_on_this()
    {
        var ctx = MatchFixtures.Context("class C { void M() { System.Threading.Monitor.Enter(this); } }");
        ResourcePatterns.WeakLock(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void WeakLock_ignores_locking_on_a_dedicated_field()
    {
        var ctx = MatchFixtures.Context(
            "class C { readonly object _lock = new object(); void M() { System.Threading.Monitor.Enter(_lock); } }");
        ResourcePatterns.WeakLock(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void WeakLockObject_reports_locking_on_a_string()
    {
        var ctx = MatchFixtures.Context("class C { void M(string key) { System.Threading.Monitor.Enter(key); } }");
        ResourcePatterns.WeakLockObject(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void WeakLockObject_ignores_locking_on_a_dedicated_field()
    {
        var ctx = MatchFixtures.Context(
            "class C { readonly object _lock = new object(); void M() { System.Threading.Monitor.Enter(_lock); } }");
        ResourcePatterns.WeakLockObject(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoExit_reports_environment_exit()
    {
        var ctx = MatchFixtures.Context("class C { void M() { System.Environment.Exit(1); } }");
        SymbolPatterns.NoExit(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void NoExit_ignores_an_unrelated_exit_method()
    {
        var ctx = MatchFixtures.Context("class C { void Exit() { } void M() { Exit(); } }");
        SymbolPatterns.NoExit(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoPathResolution_reports_process_start()
    {
        var ctx = MatchFixtures.Context("class C { void M() { System.Diagnostics.Process.Start(\"notepad\"); } }");
        SymbolPatterns.NoPathResolution(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void NoPathResolution_ignores_an_unrelated_start_method()
    {
        var ctx = MatchFixtures.Context(
            "class Timer { public void Start() { } } class C { void M() { new Timer().Start(); } }");
        SymbolPatterns.NoPathResolution(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoThreadSuspend_reports_thread_suspend()
    {
        var ctx = MatchFixtures.Context(
            "class C { void M() { System.Threading.Thread.CurrentThread.Suspend(); } }");
        SymbolPatterns.NoThreadSuspend(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void NoThreadSuspend_ignores_an_unrelated_suspend_method()
    {
        var ctx = MatchFixtures.Context(
            "class Worker { public void Suspend() { } } class C { void M() { new Worker().Suspend(); } }");
        SymbolPatterns.NoThreadSuspend(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void PreferAssemblyLoad_reports_assembly_load_from()
    {
        var ctx = MatchFixtures.Context(
            "class C { void M() { System.Reflection.Assembly.LoadFrom(\"path\"); } }");
        SymbolPatterns.PreferAssemblyLoad(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void PreferAssemblyLoad_ignores_an_unrelated_load_from_method()
    {
        var ctx = MatchFixtures.Context(
            "class Loader { public void LoadFrom(string path) { } } class C { void M() { new Loader().LoadFrom(\"path\"); } }");
        SymbolPatterns.PreferAssemblyLoad(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoDebugInProduction_reports_debugger_launch()
    {
        var ctx = MatchFixtures.Context("class C { void M() { System.Diagnostics.Debugger.Launch(); } }");
        SymbolPatterns.NoDebugInProduction(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void NoDebugInProduction_ignores_an_unrelated_launch_method()
    {
        var ctx = MatchFixtures.Context(
            "class Rocket { public void Launch() { } } class C { void M() { new Rocket().Launch(); } }");
        SymbolPatterns.NoDebugInProduction(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void UnpredictableRandom_reports_random_next()
    {
        var ctx = MatchFixtures.Context("class C { void M() { var r = new System.Random(); r.Next(); } }");
        SymbolPatterns.UnpredictableRandom(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void UnpredictableRandom_ignores_an_unrelated_next_method()
    {
        var ctx = MatchFixtures.Context(
            "class LinkedListNode { public LinkedListNode Next() => this; } class C { void M() { new LinkedListNode().Next(); } }");
        SymbolPatterns.UnpredictableRandom(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void HttpClientFactoryCreate_reports_a_call_on_the_factory_interface()
    {
        var ctx = MatchFixtures.Context(
            """
            interface IHttpClientFactory { object CreateClient(); }
            class C { void M(IHttpClientFactory factory) { factory.CreateClient(); } }
            """);
        SymbolPatterns.HttpClientFactoryCreate(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void HttpClientFactoryCreate_ignores_an_unrelated_create_client_method()
    {
        var ctx = MatchFixtures.Context(
            """
            class Builder { public Builder CreateClient() => this; }
            class C { void M(Builder b) { b.CreateClient(); } }
            """);
        SymbolPatterns.HttpClientFactoryCreate(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ProducesResponseType_reports_ok_on_a_controller_base()
    {
        var ctx = MatchFixtures.Context(
            """
            class ControllerBase { protected object Ok(object value) => value; }
            class MyController : ControllerBase { object M() => Ok(1); }
            """);
        SymbolPatterns.ProducesResponseType(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void ProducesResponseType_ignores_an_unrelated_ok_method()
    {
        var ctx = MatchFixtures.Context(
            """
            class Result { public static Result Ok(object value) => new Result(); }
            class C { void M() { Result.Ok(1); } }
            """);
        SymbolPatterns.ProducesResponseType(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    private const string LoggerInterface =
        "interface ILogger { void Information(string template, params object[] args); void Log(string message); void LogError(string message); }";

    [Fact]
    public void ConstantLogTemplate_reports_a_dynamically_built_template()
    {
        var ctx = MatchFixtures.Context(
            LoggerInterface + "\nclass C { void M(ILogger logger, string name) { logger.Information(\"Hello \" + name); } }");
        SymbolPatterns.ConstantLogTemplate(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void ConstantLogTemplate_ignores_a_literal_template()
    {
        var ctx = MatchFixtures.Context(
            LoggerInterface + "\nclass C { void M(ILogger logger, string name) { logger.Information(\"Hello {Name}\", name); } }");
        SymbolPatterns.ConstantLogTemplate(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ConstantLogTemplate_ignores_an_unrelated_information_method()
    {
        var ctx = MatchFixtures.Context(
            "class Widget { public void Information(string s) { } } class C { void M(Widget w, string name) { w.Information(\"Hello \" + name); } }");
        SymbolPatterns.ConstantLogTemplate(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void LogArgumentPosition_reports_a_mismatched_argument_count()
    {
        var ctx = MatchFixtures.Context(
            LoggerInterface + "\nclass C { void M(ILogger logger, string name) { logger.Information(\"Hello {Name}\", name, name); } }");
        SymbolPatterns.LogArgumentPosition(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void LogArgumentPosition_ignores_a_matching_argument_count()
    {
        var ctx = MatchFixtures.Context(
            LoggerInterface + "\nclass C { void M(ILogger logger, string name) { logger.Information(\"Hello {Name}\", name); } }");
        SymbolPatterns.LogArgumentPosition(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    private const string ExceptionFirstLoggerInterface =
        "interface ILogger { void Information(System.Exception ex, string template, params object[] args); void Information(string template, params object[] args); void Log(string message); void LogError(string message); }";

    [Fact]
    public void LogArgumentPosition_ignores_a_matching_argument_count_on_an_exception_first_overload()
    {
        var ctx = MatchFixtures.Context(
            ExceptionFirstLoggerInterface
            + "\nclass C { void M(ILogger logger, System.Exception ex, string id) { logger.Information(ex, \"Processing {Id}\", id); } }");
        SymbolPatterns.LogArgumentPosition(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ConstantLogTemplate_ignores_a_literal_template_on_an_exception_first_overload()
    {
        var ctx = MatchFixtures.Context(
            ExceptionFirstLoggerInterface
            + "\nclass C { void M(ILogger logger, System.Exception ex, string id) { logger.Information(ex, \"Processing {Id}\", id); } }");
        SymbolPatterns.ConstantLogTemplate(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void LogPlaceholderOrder_reports_descending_numeric_placeholders()
    {
        var ctx = MatchFixtures.Context(
            LoggerInterface + "\nclass C { void M(ILogger logger, string a, string b) { logger.Information(\"{1} {0}\", a, b); } }");
        SymbolPatterns.LogPlaceholderOrder(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void LogPlaceholderOrder_ignores_ascending_numeric_placeholders()
    {
        var ctx = MatchFixtures.Context(
            LoggerInterface + "\nclass C { void M(ILogger logger, string a, string b) { logger.Information(\"{0} {1}\", a, b); } }");
        SymbolPatterns.LogPlaceholderOrder(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void LogPlaceholderPascalCase_reports_a_camel_case_placeholder()
    {
        var ctx = MatchFixtures.Context(
            LoggerInterface + "\nclass C { void M(ILogger logger, string name) { logger.Information(\"Hello {userName}\", name); } }");
        SymbolPatterns.LogPlaceholderPascalCase(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void LogPlaceholderPascalCase_ignores_a_pascal_case_placeholder()
    {
        var ctx = MatchFixtures.Context(
            LoggerInterface + "\nclass C { void M(ILogger logger, string name) { logger.Information(\"Hello {UserName}\", name); } }");
        SymbolPatterns.LogPlaceholderPascalCase(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void LogTemplateSyntax_reports_an_unbalanced_brace()
    {
        var ctx = MatchFixtures.Context(
            LoggerInterface + "\nclass C { void M(ILogger logger, string name) { logger.Information(\"Hello {Name\", name); } }");
        SymbolPatterns.LogTemplateSyntax(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void LogTemplateSyntax_ignores_balanced_braces()
    {
        var ctx = MatchFixtures.Context(
            LoggerInterface + "\nclass C { void M(ILogger logger, string name) { logger.Information(\"Hello {Name}\", name); } }");
        SymbolPatterns.LogTemplateSyntax(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void UniqueLogPlaceholder_reports_a_duplicate_name()
    {
        var ctx = MatchFixtures.Context(
            LoggerInterface + "\nclass C { void M(ILogger logger, string a, string b) { logger.Information(\"{Name} {Name}\", a, b); } }");
        SymbolPatterns.UniqueLogPlaceholder(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void UniqueLogPlaceholder_ignores_distinct_names()
    {
        var ctx = MatchFixtures.Context(
            LoggerInterface + "\nclass C { void M(ILogger logger, string a, string b) { logger.Information(\"{FirstName} {LastName}\", a, b); } }");
        SymbolPatterns.UniqueLogPlaceholder(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void LogCaughtException_reports_a_log_call_missing_the_exception()
    {
        var ctx = MatchFixtures.Context(
            LoggerInterface + "\nclass C { void M(ILogger logger) { try { } catch (System.Exception ex) { logger.Log(\"failed\"); } } }");
        SymbolPatterns.LogCaughtException(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void LogCaughtException_ignores_a_log_call_referencing_the_exception()
    {
        var ctx = MatchFixtures.Context(
            LoggerInterface + "\nclass C { void M(ILogger logger) { try { } catch (System.Exception ex) { logger.Log(ex.Message); } } }");
        SymbolPatterns.LogCaughtException(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void LogOrRethrow_reports_logging_and_rethrowing_together()
    {
        var ctx = MatchFixtures.Context(
            LoggerInterface + "\nclass C { void M(ILogger logger) { try { } catch (System.Exception ex) { logger.Log(ex.Message); throw; } } }");
        SymbolPatterns.LogOrRethrow(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void LogOrRethrow_ignores_logging_without_rethrowing()
    {
        var ctx = MatchFixtures.Context(
            LoggerInterface + "\nclass C { void M(ILogger logger) { try { } catch (System.Exception ex) { logger.Log(ex.Message); } } }");
        SymbolPatterns.LogOrRethrow(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void AzureFunctionLogsFailure_reports_an_azure_function_catch_that_does_not_log()
    {
        var ctx = MatchFixtures.Context(
            LoggerInterface
            + "\nclass C { [FunctionName(\"Run\")] void M(ILogger logger) { try { } catch (System.Exception) { throw; } } }");
        SymbolPatterns.AzureFunctionLogsFailure(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void AzureFunctionLogsFailure_ignores_an_azure_function_catch_that_logs()
    {
        var ctx = MatchFixtures.Context(
            LoggerInterface
            + "\nclass C { [FunctionName(\"Run\")] void M(ILogger logger) { try { } catch (System.Exception ex) { logger.LogError(ex.Message); } } }");
        SymbolPatterns.AzureFunctionLogsFailure(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void AzureFunctionLogsFailure_ignores_a_catch_outside_an_azure_function()
    {
        var ctx = MatchFixtures.Context(
            LoggerInterface
            + "\nclass C { void M(ILogger logger) { try { } catch (System.Exception) { throw; } } }");
        SymbolPatterns.AzureFunctionLogsFailure(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoInterfaceCast_reports_get_type_on_an_interface_receiver()
    {
        var ctx = MatchFixtures.Context(
            "interface IFoo { } class Foo : IFoo { } class C { void M(IFoo f) { var t = f.GetType(); } }");
        SymbolPatterns.NoInterfaceCast(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void NoInterfaceCast_ignores_get_type_on_a_concrete_receiver()
    {
        var ctx = MatchFixtures.Context("class Foo { } class C { void M(Foo f) { var t = f.GetType(); } }");
        SymbolPatterns.NoInterfaceCast(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void SimplifyTypeCheck_reports_get_type_compared_to_typeof()
    {
        var ctx = MatchFixtures.Context("class Foo { } class C { bool M(object o) => o.GetType() == typeof(Foo); }");
        SymbolPatterns.SimplifyTypeCheck(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void SimplifyTypeCheck_ignores_an_is_pattern()
    {
        var ctx = MatchFixtures.Context("class Foo { } class C { bool M(object o) => o is Foo; }");
        SymbolPatterns.SimplifyTypeCheck(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoReferenceEqualsValue_reports_value_type_arguments()
    {
        var ctx = MatchFixtures.Context("class C { bool M(int a, int b) => object.ReferenceEquals(a, b); }");
        SymbolPatterns.NoReferenceEqualsValue(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void NoReferenceEqualsValue_ignores_reference_type_arguments()
    {
        var ctx = MatchFixtures.Context("class C { bool M(object a, object b) => object.ReferenceEquals(a, b); }");
        SymbolPatterns.NoReferenceEqualsValue(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void RedundantNullableCompare_reports_has_value_compared_to_true()
    {
        var ctx = MatchFixtures.Context("class C { bool M(int? x) => x.HasValue == true; }");
        SymbolPatterns.RedundantNullableCompare(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void RedundantNullableCompare_ignores_a_plain_has_value_check()
    {
        var ctx = MatchFixtures.Context("class C { bool M(int? x) => x.HasValue; }");
        SymbolPatterns.RedundantNullableCompare(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoTypeOnType_reports_get_type_on_a_system_type_receiver()
    {
        var ctx = MatchFixtures.Context("class C { System.Type M(System.Type t) => t.GetType(); }");
        SymbolPatterns.NoTypeOnType(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void NoTypeOnType_ignores_get_type_on_an_unrelated_receiver()
    {
        var ctx = MatchFixtures.Context("class Foo { } class C { System.Type M(Foo f) => f.GetType(); }");
        SymbolPatterns.NoTypeOnType(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NormalizeUppercase_reports_to_lower_on_a_string()
    {
        var ctx = MatchFixtures.Context("class C { string M(string s) => s.ToLower(); }");
        SymbolPatterns.NormalizeUppercase(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void NormalizeUppercase_ignores_to_lower_on_an_unrelated_type()
    {
        var ctx = MatchFixtures.Context("class Widget { public Widget ToLower() => this; } class C { void M(Widget w) { w.ToLower(); } }");
        SymbolPatterns.NormalizeUppercase(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void StringCulture_reports_to_lower_on_a_string()
    {
        var ctx = MatchFixtures.Context("class C { string M(string s) => s.ToLower(); }");
        SymbolPatterns.StringCulture(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void StringCulture_ignores_to_lower_on_an_unrelated_type()
    {
        var ctx = MatchFixtures.Context("class Widget { public Widget ToLower() => this; } class C { void M(Widget w) { w.ToLower(); } }");
        SymbolPatterns.StringCulture(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoExplicitForeachCast_reports_enumerable_cast_as_a_foreach_source()
    {
        var ctx = MatchFixtures.Context(
            """
            using System.Collections;
            using System.Linq;
            class C { void M(IEnumerable items) { foreach (var item in items.Cast<int>()) { } } }
            """);
        SymbolPatterns.NoExplicitForeachCast(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void NoExplicitForeachCast_ignores_an_unrelated_cast_method()
    {
        var ctx = MatchFixtures.Context(
            """
            class Caster { public Caster Cast<T>() => this; }
            class C { void M(Caster c) { foreach (var item in c.Cast<int>()) { } } }
            """);
        SymbolPatterns.NoExplicitForeachCast(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void SumOverflowCheck_reports_enumerable_sum_inside_unchecked()
    {
        var ctx = MatchFixtures.Context(
            """
            using System.Collections.Generic;
            using System.Linq;
            class C { int M(List<int> items) { unchecked { return items.Sum(); } } }
            """);
        SymbolPatterns.SumOverflowCheck(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void SumOverflowCheck_ignores_enumerable_sum_outside_unchecked()
    {
        var ctx = MatchFixtures.Context(
            """
            using System.Collections.Generic;
            using System.Linq;
            class C { int M(List<int> items) => items.Sum(); }
            """);
        SymbolPatterns.SumOverflowCheck(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void DisposeImplementsInterface_reports_a_dispose_method_on_a_non_disposable_class()
    {
        var ctx = MatchFixtures.Context("class C { public void Dispose() { } }");
        ResourcePatterns.DisposeImplementsInterface(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void DisposeImplementsInterface_ignores_a_dispose_method_implementing_idisposable()
    {
        var ctx = MatchFixtures.Context(
            "class C : System.IDisposable { public void Dispose() { System.GC.SuppressFinalize(this); } }");
        ResourcePatterns.DisposeImplementsInterface(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void DisposablePattern_reports_dispose_without_suppress_finalize()
    {
        var ctx = MatchFixtures.Context("class C : System.IDisposable { public void Dispose() { } }");
        ResourcePatterns.DisposablePattern(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void DisposablePattern_ignores_dispose_with_suppress_finalize()
    {
        var ctx = MatchFixtures.Context(
            "class C : System.IDisposable { public void Dispose() { System.GC.SuppressFinalize(this); } }");
        ResourcePatterns.DisposablePattern(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void DisposableFinalizer_reports_a_disposable_class_with_an_unmanaged_handle_and_no_finalizer()
    {
        var ctx = MatchFixtures.Context(
            "class C : System.IDisposable { System.IntPtr handle; public void Dispose() { System.GC.SuppressFinalize(this); } }");
        ResourcePatterns.DisposableFinalizer(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void DisposableFinalizer_ignores_a_disposable_class_with_an_unmanaged_handle_and_a_finalizer()
    {
        var ctx = MatchFixtures.Context(
            "class C : System.IDisposable { System.IntPtr handle; ~C() { } public void Dispose() { System.GC.SuppressFinalize(this); } }");
        ResourcePatterns.DisposableFinalizer(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void DisposableFinalizer_ignores_a_managed_only_disposable_class_with_no_finalizer()
    {
        var ctx = MatchFixtures.Context(
            "class C : System.IDisposable { System.IO.MemoryStream stream = new System.IO.MemoryStream(); public void Dispose() { stream.Dispose(); System.GC.SuppressFinalize(this); } }");
        ResourcePatterns.DisposableFinalizer(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void DisposableMembers_reports_a_disposable_field_without_idisposable()
    {
        var ctx = MatchFixtures.Context("class C { System.IO.MemoryStream stream = new System.IO.MemoryStream(); }");
        ResourcePatterns.DisposableMembers(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void DisposableMembers_ignores_a_class_that_already_implements_idisposable()
    {
        var ctx = MatchFixtures.Context(
            """
            class C : System.IDisposable
            {
                System.IO.MemoryStream stream = new System.IO.MemoryStream();
                public void Dispose() { stream.Dispose(); System.GC.SuppressFinalize(this); }
            }
            """);
        ResourcePatterns.DisposableMembers(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void DisposeOwnMembers_reports_a_dispose_method_that_skips_a_disposable_field()
    {
        var ctx = MatchFixtures.Context(
            """
            class C : System.IDisposable
            {
                System.IO.MemoryStream stream = new System.IO.MemoryStream();
                public void Dispose() { System.GC.SuppressFinalize(this); }
            }
            """);
        ResourcePatterns.DisposeOwnMembers(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void DisposeOwnMembers_ignores_a_dispose_method_that_disposes_its_field()
    {
        var ctx = MatchFixtures.Context(
            """
            class C : System.IDisposable
            {
                System.IO.MemoryStream stream = new System.IO.MemoryStream();
                public void Dispose() { stream.Dispose(); System.GC.SuppressFinalize(this); }
            }
            """);
        ResourcePatterns.DisposeOwnMembers(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void DisposeOwnMembers_ignores_a_null_conditional_dispose_call()
    {
        var ctx = MatchFixtures.Context(
            """
            class C : System.IDisposable
            {
                System.IO.MemoryStream stream = new System.IO.MemoryStream();
                public void Dispose() { stream?.Dispose(); System.GC.SuppressFinalize(this); }
            }
            """);
        ResourcePatterns.DisposeOwnMembers(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoDoubleDispose_reports_a_second_dispose_call_on_the_same_variable()
    {
        var ctx = MatchFixtures.Context(
            "class C { void M() { var s = new System.IO.MemoryStream(); s.Dispose(); s.Dispose(); } }");
        ResourcePatterns.NoDoubleDispose(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void NoDoubleDispose_ignores_a_single_dispose_call()
    {
        var ctx = MatchFixtures.Context(
            "class C { void M() { var s = new System.IO.MemoryStream(); s.Dispose(); } }");
        ResourcePatterns.NoDoubleDispose(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoDoubleDispose_ignores_dispose_calls_in_mutually_exclusive_branches()
    {
        var ctx = MatchFixtures.Context(
            """
            class C
            {
                void M(System.IDisposable r, bool c)
                {
                    if (c) { r.Dispose(); } else { r.Dispose(); }
                }
            }
            """);
        ResourcePatterns.NoDoubleDispose(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ConsumeValueTask_reports_as_task_on_a_value_task()
    {
        var ctx = MatchFixtures.Context(
            "class C { System.Threading.Tasks.Task M(System.Threading.Tasks.ValueTask vt) => vt.AsTask(); }");
        SymbolPatterns.ConsumeValueTask(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void ConsumeValueTask_ignores_an_unrelated_as_task_method()
    {
        var ctx = MatchFixtures.Context("class Wrapper { public Wrapper AsTask() => this; } class C { void M(Wrapper w) { w.AsTask(); } }");
        SymbolPatterns.ConsumeValueTask(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoBlockingAsync_reports_get_result_on_a_task_awaiter()
    {
        var ctx = MatchFixtures.Context(
            "class C { int M(System.Runtime.CompilerServices.TaskAwaiter<int> awaiter) => awaiter.GetResult(); }");
        SymbolPatterns.NoBlockingAsync(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void NoBlockingAsync_ignores_an_unrelated_get_result_method()
    {
        var ctx = MatchFixtures.Context("class Widget { public int GetResult() => 0; } class C { int M(Widget w) => w.GetResult(); }");
        SymbolPatterns.NoBlockingAsync(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoBlockingAsyncFunction_reports_wait_on_a_task()
    {
        var ctx = MatchFixtures.Context("class C { void M(System.Threading.Tasks.Task task) => task.Wait(); }");
        SymbolPatterns.NoBlockingAsyncFunction(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void NoBlockingAsyncFunction_ignores_an_unrelated_wait_method()
    {
        var ctx = MatchFixtures.Context("class Timer { public void Wait() { } } class C { void M(Timer t) => t.Wait(); }");
        SymbolPatterns.NoBlockingAsyncFunction(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ReuseClient_reports_send_async_on_an_http_client_in_an_azure_function()
    {
        var ctx = MatchFixtures.Context(
            """
            class C
            {
                [FunctionName("Run")]
                System.Threading.Tasks.Task M(System.Net.Http.HttpClient client, System.Net.Http.HttpRequestMessage request) =>
                    client.SendAsync(request);
            }
            """);
        SymbolPatterns.ReuseClient(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void ReuseClient_ignores_an_unrelated_send_async_method()
    {
        var ctx = MatchFixtures.Context(
            "class Bus { public void SendAsync(object message) { } } class C { [FunctionName(\"Run\")] void M(Bus bus, object message) => bus.SendAsync(message); }");
        SymbolPatterns.ReuseClient(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ReuseClient_ignores_an_http_client_outside_an_azure_function()
    {
        var ctx = MatchFixtures.Context(
            """
            class C
            {
                System.Threading.Tasks.Task M(System.Net.Http.HttpClient client, System.Net.Http.HttpRequestMessage request) =>
                    client.SendAsync(request);
            }
            """);
        SymbolPatterns.ReuseClient(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoReflectionAccessibility_reports_get_field_with_non_public_binding_flags()
    {
        var ctx = MatchFixtures.Context(
            """
            class C
            {
                System.Reflection.FieldInfo M(System.Type t) =>
                    t.GetField("secret", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            }
            """);
        SymbolPatterns.NoReflectionAccessibility(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void NoReflectionAccessibility_ignores_get_field_with_public_binding_flags()
    {
        var ctx = MatchFixtures.Context(
            """
            class C
            {
                System.Reflection.FieldInfo M(System.Type t) =>
                    t.GetField("visible", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            }
            """);
        SymbolPatterns.NoReflectionAccessibility(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void CheckStreamRead_reports_a_discarded_read_result()
    {
        var ctx = MatchFixtures.Context(
            "class C { void M(System.IO.Stream stream, byte[] buffer) { stream.Read(buffer, 0, buffer.Length); } }");
        SymbolPatterns.CheckStreamRead(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void CheckStreamRead_ignores_a_used_read_result()
    {
        var ctx = MatchFixtures.Context(
            "class C { int M(System.IO.Stream stream, byte[] buffer) => stream.Read(buffer, 0, buffer.Length); }");
        SymbolPatterns.CheckStreamRead(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoEmptyNullableAccess_reports_value_on_a_nullable()
    {
        var ctx = MatchFixtures.Context("class C { int M(int? x) => x.Value; }");
        SymbolPatterns.NoEmptyNullableAccess(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void NoEmptyNullableAccess_ignores_get_value_or_default()
    {
        var ctx = MatchFixtures.Context("class C { int M(int? x) => x.GetValueOrDefault(); }");
        SymbolPatterns.NoEmptyNullableAccess(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoEmptyNullableAccess_ignores_a_ternary_guarded_by_is_not_null()
    {
        var ctx = MatchFixtures.Context(
            "class C { int M(int? x) => x is not null ? x.Value : 0; }");
        SymbolPatterns.NoEmptyNullableAccess(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoEmptyNullableAccess_ignores_an_if_guarded_by_has_value()
    {
        var ctx = MatchFixtures.Context(
            "class C { int M(int? x) { if (x.HasValue) { return x.Value; } return 0; } }");
        SymbolPatterns.NoEmptyNullableAccess(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoEmptyNullableAccess_ignores_an_and_chain_guarded_by_not_null()
    {
        var ctx = MatchFixtures.Context(
            "class C { int M(bool flag, int? x) => flag && x != null ? x.Value : 0; }");
        SymbolPatterns.NoEmptyNullableAccess(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoEmptyNullableAccess_ignores_a_ternary_whenfalse_guarded_by_equals_null()
    {
        var ctx = MatchFixtures.Context(
            "class C { int M(int? x) => x == null ? 0 : x.Value; }");
        SymbolPatterns.NoEmptyNullableAccess(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoEmptyNullableAccess_ignores_an_else_branch_guarded_by_equals_null()
    {
        var ctx = MatchFixtures.Context(
            "class C { int M(int? x) { if (x == null) { return 0; } else { return x.Value; } } }");
        SymbolPatterns.NoEmptyNullableAccess(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoEmptyNullableAccess_ignores_an_or_chain_guarded_by_equals_null()
    {
        var ctx = MatchFixtures.Context(
            "class C { int M(int? x) => x == null || x.Value > 0; }");
        SymbolPatterns.NoEmptyNullableAccess(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoEmptyNullableAccess_ignores_an_early_return_guard_clause()
    {
        var ctx = MatchFixtures.Context(
            "class C { int M(int? x) { if (!x.HasValue) return 0; return x.Value; } }");
        SymbolPatterns.NoEmptyNullableAccess(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void NoEmptyNullableAccess_reports_access_after_an_unrelated_preceding_statement()
    {
        var ctx = MatchFixtures.Context(
            "class C { int M(int? x) { System.Console.WriteLine(); return x.Value; } }");
        SymbolPatterns.NoEmptyNullableAccess(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void NoPlaintextPassword_reports_compute_hash_on_a_hash_algorithm()
    {
        var ctx = MatchFixtures.Context(
            "class C { byte[] M(System.Security.Cryptography.MD5 hasher, byte[] data) => hasher.ComputeHash(data); }");
        SymbolPatterns.NoPlaintextPassword(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void NoPlaintextPassword_ignores_an_unrelated_compute_hash_method()
    {
        var ctx = MatchFixtures.Context(
            "class Widget { public byte[] ComputeHash(byte[] data) => data; } class C { byte[] M(Widget w, byte[] data) => w.ComputeHash(data); }");
        SymbolPatterns.NoPlaintextPassword(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void PasswordSalt_reports_compute_hash_on_a_hash_algorithm()
    {
        var ctx = MatchFixtures.Context(
            "class C { byte[] M(System.Security.Cryptography.SHA256 hasher, byte[] data) => hasher.ComputeHash(data); }");
        SymbolPatterns.PasswordSalt(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void PasswordSalt_ignores_an_unrelated_compute_hash_method()
    {
        var ctx = MatchFixtures.Context(
            "class Widget { public byte[] ComputeHash(byte[] data) => data; } class C { byte[] M(Widget w, byte[] data) => w.ComputeHash(data); }");
        SymbolPatterns.PasswordSalt(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void LdapAuthenticated_reports_bind_on_a_directory_entry()
    {
        var ctx = MatchFixtures.Context(
            "class DirectoryEntry { public void Bind() { } } class C { void M(DirectoryEntry entry) => entry.Bind(); }");
        SymbolPatterns.LdapAuthenticated(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void LdapAuthenticated_ignores_an_unrelated_bind_method()
    {
        var ctx = MatchFixtures.Context("class Rope { public void Bind() { } } class C { void M(Rope r) => r.Bind(); }");
        SymbolPatterns.LdapAuthenticated(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void SealedProtected_reports_protected_method_with_no_override()
    {
        var ctx = MatchFixtures.Context("sealed class C { protected void M() { } }");
        Patterns.SealedProtected(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void SealedProtected_ignores_protected_override_required_by_the_base_member()
    {
        var ctx = MatchFixtures.Context(
            """
            abstract class Base
            {
                protected abstract void M();
            }

            sealed class C : Base
            {
                protected override void M() { }
            }
            """);
        Patterns.SealedProtected(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void TestAssert_ignores_method_without_a_test_attribute()
    {
        var ctx = MatchFixtures.Context("class C { void M() { } }");
        Patterns.TestAssert(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void TestAssert_ignores_method_with_an_unrelated_attribute()
    {
        var ctx = MatchFixtures.Context("class C { [System.Obsolete] void M() { } }");
        Patterns.TestAssert(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void TestCase_ignores_class_without_a_test_suffix()
    {
        var ctx = MatchFixtures.Context("class Foo { }");
        Patterns.TestCase(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void TestSignature_ignores_method_without_a_test_attribute()
    {
        var ctx = MatchFixtures.Context("class C { void M() { } }");
        Patterns.TestSignature(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void PublicField_ignores_static_class_with_only_public_const_fields()
    {
        var ctx = MatchFixtures.Context("static class C { public const int N = 1; public const int M = 2; }");
        Patterns.PublicField(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void PublicField_flags_static_class_with_public_field_and_other_members()
    {
        var ctx = MatchFixtures.Context("static class C { public const int N = 1; public static void M() { } }");
        Patterns.PublicField(ctx, MatchFixtures.Id);
        Assert.NotEmpty(ctx.Findings);
    }

    [Fact]
    public void PublicField_flags_non_static_class_with_a_public_field()
    {
        var ctx = MatchFixtures.Context("class C { public int Value; }");
        Patterns.PublicField(ctx, MatchFixtures.Id);
        Assert.NotEmpty(ctx.Findings);
    }

    [Fact]
    public void PublicField_ignores_static_class_with_consts_and_a_public_static_field()
    {
        var ctx = MatchFixtures.Context(
            "static class C { public const string A = \"a\"; public static readonly string[] Keys = [A]; }");
        Patterns.PublicField(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void FieldPrivate_ignores_static_class_with_consts_and_a_public_static_field()
    {
        var ctx = MatchFixtures.Context(
            "static class C { public const string A = \"a\"; public static readonly string[] Keys = [A]; }");
        Patterns.FieldPrivate(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void FieldPrivate_flags_static_class_with_a_public_field_and_a_method()
    {
        var ctx = MatchFixtures.Context(
            "static class C { public static readonly string[] Keys = []; public static void M() { } }");
        Patterns.FieldPrivate(ctx, MatchFixtures.Id);
        Assert.NotEmpty(ctx.Findings);
    }

    [Fact]
    public void FieldPrivate_flags_non_static_class_with_a_public_field()
    {
        var ctx = MatchFixtures.Context("class C { public readonly int Value; }");
        Patterns.FieldPrivate(ctx, MatchFixtures.Id);
        Assert.NotEmpty(ctx.Findings);
    }

    [Fact]
    public void AsyncSuffix_ignores_the_entry_point_Main_method()
    {
        var ctx = MatchFixtures.Context("class Program { static async System.Threading.Tasks.Task Main() { } }");
        Patterns.AsyncSuffix(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void AsyncSuffix_flags_other_async_methods_missing_the_suffix()
    {
        var ctx = MatchFixtures.Context("class C { async System.Threading.Tasks.Task Run() { } }");
        Patterns.AsyncSuffix(ctx, MatchFixtures.Id);
        Assert.NotEmpty(ctx.Findings);
    }

    [Fact]
    public void DisposeCreated_ignores_a_disposable_returned_from_a_lambda_expression_body()
    {
        var ctx = MatchFixtures.Context(
            "using System; using System.IO; class C { void M(Func<int, Stream> f) { M(x => new MemoryStream()); } }");
        Patterns.DisposeCreated(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void DisposeCreated_flags_a_disposable_created_and_discarded_inside_a_lambda_block()
    {
        var ctx = MatchFixtures.Context(
            "using System; using System.IO; class C { void M(Action a) { M(() => { new MemoryStream(); }); } }");
        Patterns.DisposeCreated(ctx, MatchFixtures.Id);
        Assert.NotEmpty(ctx.Findings);
    }

    [Fact]
    public void ExceptionChecks_ignore_a_call_to_Record_Exception()
    {
        var ctx = MatchFixtures.Context(
            """
            class C
            {
                void M()
                {
                    var exception = Record.Exception(() => Work());
                }

                static void Work() { }
            }
            """);

        Patterns.ExceptionNameExtends(ctx, MatchFixtures.Id);
        Patterns.ExceptionPublic(ctx, MatchFixtures.Id);
        Patterns.ExceptionStandardConstructors(ctx, MatchFixtures.Id);
        Patterns.ThrowCreatedException(ctx, MatchFixtures.Id);

        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ExceptionNameExtends_flags_a_class_named_like_exception_that_does_not_extend_exception()
    {
        var ctx = MatchFixtures.Context("public class FooException { }");
        Patterns.ExceptionNameExtends(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void ExceptionNameExtends_ignores_a_class_that_extends_exception()
    {
        var ctx = MatchFixtures.Context("public class FooException : Exception { public FooException() { } }");
        Patterns.ExceptionNameExtends(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ExceptionPublic_flags_an_internal_exception_type()
    {
        var ctx = MatchFixtures.Context("internal sealed class FooException : Exception { }");
        Patterns.ExceptionPublic(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void ExceptionPublic_ignores_a_public_exception_type()
    {
        var ctx = MatchFixtures.Context("public sealed class FooException : Exception { }");
        Patterns.ExceptionPublic(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ExceptionStandardConstructors_flags_a_type_missing_the_standard_constructors()
    {
        var ctx = MatchFixtures.Context("public class FooException : Exception { }");
        Patterns.ExceptionStandardConstructors(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void ExceptionStandardConstructors_ignores_a_type_with_all_standard_constructors()
    {
        var ctx = MatchFixtures.Context(
            """
            public class FooException : Exception
            {
                public FooException() { }

                public FooException(string message) : base(message) { }

                public FooException(string message, Exception innerException) : base(message, innerException) { }
            }
            """);
        Patterns.ExceptionStandardConstructors(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void ThrowCreatedException_flags_a_discarded_exception_object()
    {
        var ctx = MatchFixtures.Context("class C { void M() { new InvalidOperationException(\"x\"); } }");
        Patterns.ThrowCreatedException(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void ThrowCreatedException_ignores_an_exception_that_is_thrown()
    {
        var ctx = MatchFixtures.Context("class C { void M() { throw new InvalidOperationException(\"x\"); } }");
        Patterns.ThrowCreatedException(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void TestCase_recognizes_a_theory_as_a_test_case()
    {
        var ctx = MatchFixtures.Context(
            """
            class WidgetTests
            {
                [Theory]
                [InlineData(1)]
                void M(int value) { }
            }
            """);
        Patterns.TestCase(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void TestCase_recognizes_a_fully_qualified_theory_attribute()
    {
        var ctx = MatchFixtures.Context(
            """
            class WidgetTests
            {
                [Xunit.TheoryAttribute]
                [Xunit.InlineData(1)]
                void M(int value) { }
            }
            """);
        Patterns.TestCase(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void TestCase_recognizes_nunit_test_case_attributes()
    {
        var ctx = MatchFixtures.Context(
            """
            class WidgetTests
            {
                [TestCase(1)]
                void M(int value) { }
            }
            """);
        Patterns.TestCase(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void TestCase_recognizes_mstest_data_test_method_attribute()
    {
        var ctx = MatchFixtures.Context(
            """
            class WidgetTests
            {
                [DataTestMethod]
                void M(int value) { }
            }
            """);
        Patterns.TestCase(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void TestCase_recognizes_a_skippable_fact_by_name_convention()
    {
        var ctx = MatchFixtures.Context(
            """
            class WidgetTests
            {
                [SkippableFact]
                void M() { }
            }
            """);
        Patterns.TestCase(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void TestCase_still_flags_a_test_class_with_no_test_methods()
    {
        var ctx = MatchFixtures.Context("class WidgetTests { void M() { } }");
        Patterns.TestCase(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }
}

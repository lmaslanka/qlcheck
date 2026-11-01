using Qlcheck.Languages.CSharp.Catalog.Dataflow;

namespace Qlcheck.Tests;

public class LockFlowTests
{
    private const string Fields = "readonly object _g = new(); readonly ReaderWriterLockSlim _l = new(); readonly SemaphoreSlim _s = new(1);";

    [Theory]
    [InlineData("Monitor.Enter(_g); try { Console.WriteLine(); } finally { Monitor.Exit(_g); }")]
    [InlineData("lock (_g) { Console.WriteLine(); }")]
    [InlineData("var taken = false; try { Monitor.Enter(_g, ref taken); } finally { if (taken) { Monitor.Exit(_g); } }")]
    [InlineData("if (!Monitor.TryEnter(_g)) { return; } try { } finally { Monitor.Exit(_g); }")]
    [InlineData("if (Monitor.TryEnter(_g)) { Monitor.Exit(_g); }")]
    [InlineData("_l.EnterReadLock(); try { } finally { _l.ExitReadLock(); }")]
    [InlineData("_s.Wait(); try { } finally { _s.Release(); }")]
    [InlineData("Monitor.Enter(_g); if (flag) { Monitor.Exit(_g); return; } Monitor.Exit(_g);")]
    public void Balanced_locks_give_nothing(string body)
    {
        Assert.Empty(Run(body));
    }

    [Theory]
    [InlineData("Monitor.Enter(_g); if (flag) { return; } Monitor.Exit(_g);", "NotReleasedOnAllPaths", "Monitor.Enter(_g)")]
    [InlineData("Monitor.Enter(_g); Console.WriteLine();", "NeverReleased", "Monitor.Enter(_g)")]
    [InlineData("_l.EnterWriteLock(); if (flag) { _l.ExitWriteLock(); }", "NotReleasedOnAllPaths", "_l.EnterWriteLock()")]
    [InlineData("_s.Wait(); if (flag) { _s.Release(); }", "NotReleasedOnAllPaths", "_s.Wait()")]
    public void A_lock_still_held_at_the_end_is_reported_at_its_acquisition(string body, string problem, string at)
    {
        var hit = Assert.Single(Run(body));

        Assert.Equal(problem, hit.Problem.ToString());
        Assert.Equal(at, hit.Node.ToString());
    }

    [Fact]
    public void Releasing_the_other_reader_writer_mode_is_reported_at_the_release()
    {
        var hits = Run("_l.EnterReadLock(); try { } finally { _l.ExitWriteLock(); }");

        var mismatch = Assert.Single(hits, hit => hit.Problem == LockProblem.ModeMismatch);
        Assert.Equal("_l.ExitWriteLock()", mismatch.Node.ToString());
    }

    [Fact]
    public void A_semaphore_released_in_another_method_is_a_throttle()
    {
        Assert.Empty(Run("_s.Wait(); Console.WriteLine();"));
    }

    [Fact]
    public void Monitor_exit_is_never_a_mode_mismatch()
    {
        Assert.DoesNotContain(Run("Monitor.Enter(_g); try { } finally { Monitor.Exit(_g); }"), hit => hit.Problem == LockProblem.ModeMismatch);
    }

    private static List<LockHit> Run(string body)
    {
        var source = $"using System; using System.Threading; public class R {{ {Fields} void M(bool flag) {{ {body} }} }}";
        var tree = CSharpTrees.Parse(new SourceFile("Repo.cs", source));
        var model = CSharpCompilations.Create("locks", [tree]).GetSemanticModel(tree);
        Assert.Empty(model.GetDiagnostics().Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
        return [.. LockFlow.Run(new BodyGraphs(tree, model))];
    }
}

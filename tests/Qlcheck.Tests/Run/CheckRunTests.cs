using Qlcheck.Checks;
using Qlcheck.Languages;
using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class CheckRunTests
{
    private const string CSharpPath = "Repo.cs";

    private const string FakePath = "Repo.fake";

    private const string CSharpFullPath = "/repo/Repo.cs";

    private const string FakeFullPath = "/repo/Repo.fake";

    private const string FakeLanguageId = "fake";

    private const string OtherLanguageId = "other";

    private const string FakeExtension = ".fake";

    private const string MissingLanguageId = "missing";

    private const string OrphanCheckId = "orphan";

    private const string NotCSharpCheckId = "not-csharp";

    private const string RanMessage = "ran";

    private const string MagicSource = """
        class C
        {
            int M()
            {
                return 42;
            }
        }
        """;

    [Fact]
    public void Runs_a_check_only_on_its_language()
    {
        var dir = Directory.CreateTempSubdirectory("qlcheck_checkrun_");
        try
        {
            var magicLiteral = Catalog.Checks.Single(c => c.Id == "magic-literal");
            var csharpFullPath = Path.Combine(dir.FullName, CSharpPath);
            var findings = CheckRun.Execute(
                [
                    new SourceScan.LoadedSource(csharpFullPath, new SourceFile(CSharpPath, MagicSource)),
                    new SourceScan.LoadedSource(FakeFullPath, new SourceFile(FakePath, MagicSource)),
                ],
                [magicLiteral, new BoundCheck(FakeLanguageId, FakeLanguageId)],
                [
                    new CSharpLanguage(),
                    new RecordingLanguage(FakeLanguageId, FakeExtension),
                ]);

            Assert.Contains(findings.Findings, finding => finding.Check == magicLiteral.Id && finding.File == CSharpPath);
            Assert.DoesNotContain(findings.Findings, finding => finding.Check == magicLiteral.Id && finding.File == FakePath);
            Assert.Contains(findings.Findings, finding => finding.Check == FakeLanguageId && finding.File == FakePath);
            Assert.DoesNotContain(findings.Findings, finding => finding.Check == FakeLanguageId && finding.File == CSharpPath);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Throws_when_two_languages_match_one_file()
    {
        var file = new SourceScan.LoadedSource(FakeFullPath, new SourceFile(FakePath, MagicSource));
        Assert.Throws<InvalidOperationException>(() =>
            CheckRun.Execute(
                [file],
                [],
                [
                    new RecordingLanguage(FakeLanguageId, FakeExtension),
                    new RecordingLanguage(OtherLanguageId, FakeExtension),
                ]));
    }

    [Fact]
    public void Throws_when_a_check_has_no_language()
    {
        var file = new SourceScan.LoadedSource(CSharpFullPath, new SourceFile(CSharpPath, MagicSource));
        Assert.Throws<InvalidOperationException>(() =>
            CheckRun.Execute(
                [file],
                [new BoundCheck(OrphanCheckId, MissingLanguageId)],
                [new CSharpLanguage()]));
    }

    [Fact]
    public void Throws_when_a_csharp_check_cannot_analyze()
    {
        var file = new SourceScan.LoadedSource(CSharpFullPath, new SourceFile(CSharpPath, MagicSource));
        Assert.Throws<InvalidOperationException>(() =>
            CheckRun.Execute(
                [file],
                [new BoundCheck(NotCSharpCheckId, CSharpLanguage.LanguageId)],
                [new CSharpLanguage()]));
    }

    [Fact]
    public void Skips_a_language_with_no_matching_files()
    {
        var file = new SourceScan.LoadedSource(CSharpFullPath, new SourceFile(CSharpPath, MagicSource));
        var result = CheckRun.Execute(
            [file],
            [new BoundCheck(FakeLanguageId, FakeLanguageId)],
            [new RecordingLanguage(FakeLanguageId, FakeExtension)]);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Skips_a_language_with_no_assigned_checks()
    {
        var file = new SourceScan.LoadedSource(FakeFullPath, new SourceFile(FakePath, MagicSource));
        var result = CheckRun.Execute(
            [file],
            [],
            [new RecordingLanguage(FakeLanguageId, FakeExtension)]);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Throws_when_a_check_matches_two_languages()
    {
        var file = new SourceScan.LoadedSource(FakeFullPath, new SourceFile(FakePath, MagicSource));
        Assert.Throws<InvalidOperationException>(() =>
            CheckRun.Execute(
                [file],
                [new BoundCheck(FakeLanguageId, FakeLanguageId)],
                [
                    new RecordingLanguage(FakeLanguageId, FakeExtension),
                    new RecordingLanguage(FakeLanguageId, FakeExtension),
                ]));
    }

    private sealed class RecordingLanguage : ILanguage
    {
        private readonly string _extension;

        public RecordingLanguage(string id, string extension)
        {
            Id = id;
            _extension = extension;
        }

        public string Id { get; }

        public bool Matches(string path) =>
            path.EndsWith(_extension, StringComparison.OrdinalIgnoreCase);

        public RunResult Execute(
            IReadOnlyList<SourceScan.LoadedSource> files,
            IReadOnlyList<ICheck> checks)
        {
            var findings = new List<Finding>();
            foreach (var file in files)
            {
                foreach (var check in checks)
                {
                    findings.Add(new Finding(
                        $"{check.Id}:{file.File.Path}:1:1",
                        check.Id,
                        file.File.Path,
                        1,
                        1,
                        RanMessage));
                }
            }

            return new RunResult(findings, []);
        }
    }

    private sealed class BoundCheck : ICheck
    {
        public BoundCheck(string id, string language)
        {
            Id = id;
            Language = language;
        }

        public string Id { get; }

        public string Language { get; }
    }
}

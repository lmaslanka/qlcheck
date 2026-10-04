namespace Qlcheck.ArchitectureTests.Rules;

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.ArchitectureTests.Support;

/// <summary>
/// Which part of qlcheck's own source may depend on which. qlcheck is a single assembly, so these are
/// `using`-directive rules over the source tree (ADR 0001's "syntax trees, not compilation" applies here
/// too), not project-reference or IL rules.
/// </summary>
public sealed class LayeringTests
{
    private static readonly Rule L1 = new(
        "L1",
        "Cli depends on nothing else in qlcheck",
        "Cli.cs only parses arguments into Options. Move any decision that needs Run/Report/Languages/Scan/Checks out of Cli and into QlcheckApp, the composition root.");

    private static readonly Rule L2 = new(
        "L2",
        "Report does not depend on Run or Languages",
        "Report renders the Findings/Coverage/ICheck/Options it is handed. If it needs to orchestrate a check or know about a Language, that decision belongs in QlcheckApp instead.");

    private static readonly Rule L3 = new(
        "L3",
        "Run does not depend on Report or Cli",
        "Run discovers and executes Checks; it has no opinion on how results are displayed or how arguments were parsed. Move presentation concerns to Report and keep Run's inputs plain data.");

    private static readonly Rule L4 = new(
        "L4",
        "Scan depends on nothing else in qlcheck",
        "Scan is the lowest layer (file-system and git access) so every other layer can depend on it safely. If a Scan file needs catalog/run/report logic, that logic belongs above Scan, not inside it.");

    private static readonly Rule L5 = new(
        "L5",
        "The Catalog does not depend on Run, Report, or Cli",
        "Catalog pattern/engine code only inspects a WalkContext and reports Findings; it must not know who called it or how results will be shown. If a check needs orchestration, add that to Run instead.");

    private static readonly Rule L6 = new(
        "L6",
        "Only GitChanges and CoverageRunner shell out to another process",
        "Everything else should be in-process (ADR 0002). If a check genuinely needs an external process, route it through one of the two sanctioned call sites instead of adding a new one.");

    [Fact]
    public void L1_CliDependsOnNothingElseInQlcheck()
    {
        Baseline.Check(L1, ForbiddenUsings("src/Qlcheck/Cli/", "Qlcheck"));
    }

    [Fact]
    public void L2_ReportDoesNotDependOnRunOrLanguages()
    {
        Baseline.Check(L2, ForbiddenUsings("src/Qlcheck/Report/", "Qlcheck.Run", "Qlcheck.Languages"));
    }

    [Fact]
    public void L3_RunDoesNotDependOnReportOrCli()
    {
        Baseline.Check(L3, ForbiddenUsings("src/Qlcheck/Run/", "Qlcheck.Report", "Qlcheck.Cli"));
    }

    [Fact]
    public void L4_ScanDependsOnNothingElseInQlcheck()
    {
        Baseline.Check(L4, ForbiddenUsings("src/Qlcheck/Scan/", "Qlcheck"));
    }

    [Fact]
    public void L5_TheCatalogDoesNotDependOnRunReportOrCli()
    {
        Baseline.Check(L5, ForbiddenUsings(
            "src/Qlcheck/Languages/CSharp/Catalog/", "Qlcheck.Run", "Qlcheck.Report", "Qlcheck.Cli"));
    }

    [Fact]
    public void L6_OnlyGitChangesAndCoverageRunnerShellOut()
    {
        Baseline.Check(L6, ProcessCreations());
    }

    private static IEnumerable<string> ForbiddenUsings(string folder, params string[] forbiddenPrefixes) =>
        from file in Sources.Under(folder)
        from name in Sources.UsingNames(file)
        from prefix in forbiddenPrefixes
        where name == prefix || name.StartsWith(prefix + ".", System.StringComparison.Ordinal)
        select $"{file.RelativePath} -> using {name};";

    private static readonly string[] SanctionedProcessFiles =
    [
        "src/Qlcheck/Scan/GitChanges.cs",
        "src/Qlcheck/Languages/CSharp/Checks/Coverage/CoverageRunner.cs",
    ];

    private static IEnumerable<string> ProcessCreations() =>
        from file in Sources.All()
        where !SanctionedProcessFiles.Contains(file.RelativePath, System.StringComparer.Ordinal)
        from creation in file.Root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
        where creation.Type.ToString() is "Process" or "ProcessStartInfo"
        select $"{file.RelativePath}:{creation.GetLocation().GetLineSpan().StartLinePosition.Line + 1} -> new {creation.Type}";
}

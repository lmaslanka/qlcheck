namespace Qlcheck.ArchitectureTests.Support;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit.Sdk;

/// <summary>
/// Ratchet for pre-existing violations. Every rule computes its current violations and compares them with
/// <c>Baseline/&lt;RuleId&gt;.txt</c>:
/// <list type="bullet">
/// <item>a violation that is not in the baseline fails the rule (new code must follow the rule);</item>
/// <item>a baseline entry that no longer occurs also fails the rule, so the file can only shrink.</item>
/// </list>
/// A rule with no pre-existing violations simply has no baseline file. Baseline files are read from the
/// source tree, never from the build output.
/// </summary>
public static class Baseline
{
    /// <summary>
    /// When set, the current violations of every rule are written to this folder (never into Baseline/). A
    /// human can use the output to review a baseline change; nothing is written back automatically.
    /// </summary>
    public const string DumpDirectoryVariable = "ARCH_BASELINE_DUMP_DIR";

    public static void Check(Rule rule, IEnumerable<string> violations)
    {
        var actual = violations
            .Select(violation => violation.Trim())
            .Where(violation => violation.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(violation => violation, StringComparer.Ordinal)
            .ToList();

        Dump(rule, actual);

        var path = PathFor(rule);
        var baseline = Load(path);
        var duplicates = baseline.GroupBy(entry => entry, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        var added = actual.Except(baseline, StringComparer.Ordinal).ToList();
        var fixedEntries = baseline.Distinct(StringComparer.Ordinal).Except(actual, StringComparer.Ordinal).ToList();

        if (added.Count == 0 && fixedEntries.Count == 0 && duplicates.Count == 0)
        {
            return;
        }

        var message = new StringBuilder();
        message.AppendLine($"Architecture rule {rule.Id} failed: {rule.Title}");

        if (added.Count > 0)
        {
            message.AppendLine();
            message.AppendLine($"{added.Count} new violation(s):");
            foreach (var violation in added)
            {
                message.AppendLine($"  - {violation}");
            }

            message.AppendLine();
            message.AppendLine($"How to fix: {rule.Guidance}");
            message.AppendLine($"Do NOT add these lines to {RepoRoot.RelativePath(path)}. The baseline only freezes legacy code; new code must comply.");
        }

        if (fixedEntries.Count > 0)
        {
            message.AppendLine();
            message.AppendLine($"{fixedEntries.Count} baseline entr{(fixedEntries.Count == 1 ? "y is" : "ies are")} no longer violated. Delete these lines from {RepoRoot.RelativePath(path)}:");
            foreach (var entry in fixedEntries)
            {
                message.AppendLine($"  - {entry}");
            }
        }

        if (duplicates.Count > 0)
        {
            message.AppendLine();
            message.AppendLine($"Duplicate baseline entries in {RepoRoot.RelativePath(path)}: {string.Join(", ", duplicates)}");
        }

        throw new XunitException(message.ToString());
    }

    public static string PathFor(Rule rule) =>
        Path.Combine(RepoRoot.Path, "tests", "Qlcheck.ArchitectureTests", "Baseline", $"{rule.Id}.txt");

    private static List<string> Load(string path) =>
        File.Exists(path)
            ? File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .ToList()
            : [];

    private static void Dump(Rule rule, IReadOnlyCollection<string> actual)
    {
        var directory = Environment.GetEnvironmentVariable(DumpDirectoryVariable);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var full = Path.GetFullPath(directory);
        if (full.StartsWith(Path.Combine(RepoRoot.Path, "tests", "Qlcheck.ArchitectureTests"), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{DumpDirectoryVariable} must not point inside the architecture test project.");
        }

        Directory.CreateDirectory(full);
        var header = $"# {rule.Id}: {rule.Title}{Environment.NewLine}# Legacy violations frozen by the architecture ratchet. Remove lines as code is fixed; never add new ones.{Environment.NewLine}";
        File.WriteAllText(Path.Combine(full, $"{rule.Id}.txt"), header + string.Join(Environment.NewLine, actual) + (actual.Count > 0 ? Environment.NewLine : string.Empty));
    }
}

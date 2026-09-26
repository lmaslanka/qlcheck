using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Scan;

namespace Qlcheck.Languages.CSharp.Checks.Coverage;

internal static class CoverageMap
{
    private const int Column = 1;

    public static CoverageAnalysis Build(
        IReadOnlyList<SourceScan.LoadedSource> files,
        LcovMap.Document hits)
    {
        var findings = new List<Finding>();
        var coverage = new List<CoverageFile>();
        foreach (var source in files)
        {
            if (!hits.TryGet(source.FullPath, out var fileHits))
            {
                AddMissing(source, findings, coverage);
                continue;
            }

            AddMeasured(source.File.Path, fileHits, findings, coverage);
        }

        return new CoverageAnalysis(findings, coverage);
    }

    private static void AddMissing(
        SourceScan.LoadedSource source,
        List<Finding> findings,
        List<CoverageFile> coverage)
    {
        if (!HasMethodBody(source.File))
        {
            coverage.Add(new CoverageFile(source.File.Path, false, []));
            return;
        }

        var line = FirstBodyLine(source.File);
        findings.Add(Finding.At(
            CoverageCheck.CheckId,
            source.File.Path,
            line,
            Column,
            CoverageCheck.MissingMessage));
        coverage.Add(new CoverageFile(source.File.Path, true, []));
    }

    private static void AddMeasured(
        string path,
        LcovMap.FileHits fileHits,
        List<Finding> findings,
        List<CoverageFile> coverage)
    {
        var uncovered = new List<LcovMap.LineHit>();
        foreach (var line in fileHits.Lines)
        {
            if (line.Hits != 0)
            {
                continue;
            }

            uncovered.Add(line);
        }

        uncovered.Sort(static (left, right) => left.Line.CompareTo(right.Line));
        foreach (var line in uncovered)
        {
            findings.Add(Finding.At(
                CoverageCheck.CheckId,
                path,
                line.Line,
                Column,
                CoverageCheck.UncoveredMessage));
        }

        coverage.Add(new CoverageFile(path, false, Methods(uncovered)));
    }

    private static List<CoverageMethod> Methods(List<LcovMap.LineHit> uncovered)
    {
        var groups = new List<CoverageMethod>();
        foreach (var line in uncovered)
        {
            var name = string.IsNullOrEmpty(line.Method) ? null : line.Method;
            var existing = groups.Find(method => method.Name == name);
            if (existing is null)
            {
                groups.Add(new CoverageMethod(name, [line.Line]));
                continue;
            }

            var lines = new List<int>(existing.Lines) { line.Line };
            groups.Remove(existing);
            groups.Add(new CoverageMethod(name, lines));
        }

        groups.Sort(static (left, right) => left.Lines[0].CompareTo(right.Lines[0]));
        return groups;
    }

    private static bool HasMethodBody(SourceFile file)
    {
        var tree = CSharpTrees.Parse(file);
        foreach (var node in tree.GetRoot().DescendantNodes())
        {
            if (node is BlockSyntax or ArrowExpressionClauseSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private static int FirstBodyLine(SourceFile file)
    {
        var tree = CSharpTrees.Parse(file);
        foreach (var node in tree.GetRoot().DescendantNodes())
        {
            if (node is not (BlockSyntax or ArrowExpressionClauseSyntax))
            {
                continue;
            }

            return node.GetLocation().GetLineSpan().StartLinePosition.Line + Column;
        }

        return Column;
    }
}

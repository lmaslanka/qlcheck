// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Qlcheck.Languages.CSharp.Catalog;

// Finds duplicated statement sequences (plan 04, D5). Units are statements, each in the sequence of
// its block or switch section, so a clone never starts or ends mid-statement and member lists,
// initializers and switch expressions never take part. Tokens are normalized: identifiers become
// one symbol, literals and everything else stay exact. Units are bucketed by hash, each pair is
// extended to the right while the units' tokens are equal, and only left-maximal pairs count. A
// region must reach the token and statement thresholds, rename identifiers consistently, not
// overlap itself and not be a run of one repeated statement shape. Groups of copies are then
// claimed biggest first, so a nested or shifted part of a bigger clone is not reported again.
internal sealed class CloneIndex
{
    private const int BucketCap = 32;

    private const int MinimumCopies = 2;

    // A run repeats a statement shape with a period that fits at least twice.
    private const int MinimumRepeats = 2;

    private const ulong Base = 1_000_003;

    private const int IdentifierToken = 100_000;

    private const int LiteralOffset = 200_000;

    private const int GeneratedHeaderLength = 2_000;

    private readonly List<FileTokens> _files = [];

    private readonly List<Unit> _units = [];

    private readonly Dictionary<string, int> _interned = new(StringComparer.Ordinal);

    private CloneIndex(IEnumerable<SourceSetFile> files)
    {
        foreach (var file in files.Where(file => !IsGenerated(file)))
        {
            Index(file);
        }
    }

    public static IReadOnlyList<CloneGroup> Find(IEnumerable<SourceSetFile> files) => new CloneIndex(files).Groups();

    private static bool IsGenerated(SourceSetFile file)
    {
        var text = file.Tree.GetText();
        return GeneratedFiles.IsGenerated(file.Path, text.ToString(new TextSpan(0, Math.Min(text.Length, GeneratedHeaderLength))));
    }

    private static bool IsLiteral(SyntaxToken token) =>
        token.Kind() is SyntaxKind.NumericLiteralToken or SyntaxKind.StringLiteralToken or SyntaxKind.CharacterLiteralToken
            or SyntaxKind.InterpolatedStringTextToken or SyntaxKind.SingleLineRawStringLiteralToken
            or SyntaxKind.MultiLineRawStringLiteralToken or SyntaxKind.Utf8StringLiteralToken;

    // The statements of a block or switch section, which form one sequence; none for other nodes.
    private static SyntaxList<StatementSyntax> StatementsOf(SyntaxNode node) =>
        node switch
        {
            BlockSyntax block => block.Statements,
            SwitchSectionSyntax section => section.Statements,
            _ => default,
        };

    private int Intern(string text)
    {
        if (!_interned.TryGetValue(text, out var id))
        {
            id = _interned.Count + 1;
            _interned[text] = id;
        }

        return id;
    }

    // An identifier becomes one symbol plus its interned name, a literal its exact text, and any
    // other token its kind.
    private (int Norm, int Raw) Normalize(SyntaxToken token)
    {
        if (token.IsKind(SyntaxKind.IdentifierToken))
        {
            return (IdentifierToken, Intern(token.ValueText));
        }

        return (IsLiteral(token) ? LiteralOffset + Intern(token.Text) : token.RawKind, 0);
    }

    private void Index(SourceSetFile file)
    {
        var tokens = file.Tree.GetRoot().DescendantTokens().Where(token => token.Span.Length > 0).ToList();
        var at = tokens.Select((token, index) => (token, index)).ToDictionary(entry => entry.token, entry => entry.index);
        var normalized = tokens.Select(Normalize).ToList();
        var tokensOfFile = new FileTokens(file, [.. normalized.Select(token => token.Norm)], [.. normalized.Select(token => token.Raw)]);
        _files.Add(tokensOfFile);
        var fileIndex = _files.Count - 1;
        foreach (var sequence in file.Tree.GetRoot().DescendantNodes().Select(StatementsOf).Where(statements => statements.Count > 0))
        {
            _units.AddRange(sequence.Select(statement => UnitOf(statement, fileIndex, tokensOfFile, at)).OfType<Unit>());
            _units.Add(Unit.Sentinel);
        }
    }

    // A statement as a unit, counting its nested non-block statements. Invalid code whose tokens
    // are missing gives no unit.
    private static Unit? UnitOf(StatementSyntax statement, int file, FileTokens tokens, Dictionary<SyntaxToken, int> at)
    {
        if (!at.TryGetValue(statement.GetFirstToken(), out var first) || !at.TryGetValue(statement.GetLastToken(), out var last))
        {
            return null;
        }

        var statements = statement.DescendantNodesAndSelf().OfType<StatementSyntax>().Count(node => node is not BlockSyntax);
        return new Unit(file, first, last, tokens.Hash(first, last), statements, statement);
    }

    private IReadOnlyList<CloneGroup> Groups()
    {
        var regions = new Dictionary<(ulong Hash, int Tokens), SortedSet<int>>();
        foreach (var (a, b, length) in Matches())
        {
            var key = (RegionHash(a, length), RegionTokens(a, length));
            if (!regions.TryGetValue(key, out var starts))
            {
                starts = [];
                regions[key] = starts;
            }

            starts.Add(a);
            starts.Add(b);
        }

        var candidates = regions
            .Select(region => (Tokens: region.Key.Tokens, Copies: region.Value.Select(start => Copy(start, Length(start, region.Key))).OrderBy(copy => copy.Path, StringComparer.Ordinal).ThenBy(copy => copy.Span.Start).ToList()))
            .OrderByDescending(group => group.Tokens)
            .ThenByDescending(group => group.Copies.Count)
            .ThenBy(group => group.Copies[0].Path, StringComparer.Ordinal)
            .ThenBy(group => group.Copies[0].Span.Start);
        return Claim(candidates);
    }

    // Biggest groups first: a copy overlapping one already claimed is dropped, and a group needs two
    // copies left to survive.
    private static IReadOnlyList<CloneGroup> Claim(IEnumerable<(int Tokens, List<CloneCopy> Copies)> candidates)
    {
        var claimed = new List<(string Path, TextSpan Span)>();
        var groups = new List<CloneGroup>();
        foreach (var (tokens, copies) in candidates)
        {
            var free = copies.Where(copy => !claimed.Any(other => other.Path == copy.Path && other.Span.OverlapsWith(copy.Span))).ToList();
            if (free.Count < MinimumCopies)
            {
                continue;
            }

            groups.Add(new CloneGroup(free, tokens));
            claimed.AddRange(free.Select(copy => (copy.Path, copy.Span)));
        }

        return [.. groups.OrderBy(group => group.Copies[0].Path, StringComparer.Ordinal).ThenBy(group => group.Copies[0].Span.Start)];
    }

    private IEnumerable<(int A, int B, int Length)> Matches()
    {
        var buckets = Enumerable.Range(0, _units.Count)
            .Where(index => !_units[index].IsSentinel)
            .GroupBy(index => _units[index].Hash)
            .Select(bucket => bucket.ToList())
            .Where(bucket => bucket.Count >= MinimumCopies);
        foreach (var bucket in buckets)
        {
            // A huge bucket of one statement shape is compared to its first entry only: linear on
            // degenerate input.
            var pairs = bucket.Count <= BucketCap
                ? bucket.SelectMany((a, x) => bucket.Skip(x + 1).Select(b => (a, b)))
                : bucket.Skip(1).Select(b => (a: bucket[0], b));
            foreach (var (a, b) in pairs)
            {
                if (Match(a, b) is { } length)
                {
                    yield return (a, b, length);
                }
            }
        }
    }

    // The length in units of the clone starting at units a and b, or null when it is not one.
    private int? Match(int a, int b)
    {
        if (a > 0 && Same(a - 1, b - 1))
        {
            return null;
        }

        var length = 0;
        while (b + length < _units.Count && Same(a + length, b + length))
        {
            length++;
        }

        return IsBigEnough(a, length) && IsExtractable(a, b, length) ? length : null;
    }

    private bool IsBigEnough(int start, int length) =>
        RegionTokens(start, length) >= Thresholds.DuplicateTokens
        && Enumerable.Range(start, length).Sum(k => _units[k].Statements) >= Thresholds.DuplicateStatements;

    // Copies that do not overlap, are not a run of one repeated shape, and rename consistently.
    private bool IsExtractable(int a, int b, int length) =>
        !Overlaps(a, b, length) && !IsPeriodic(a, length) && RenamesConsistently(a, b, length);

    private bool Same(int x, int y)
    {
        var (first, second) = (_units[x], _units[y]);
        return !first.IsSentinel && !second.IsSentinel && first.Hash == second.Hash
            && _files[first.File].Norm.AsSpan(first.First, first.Last - first.First + 1)
                .SequenceEqual(_files[second.File].Norm.AsSpan(second.First, second.Last - second.First + 1));
    }

    private bool Overlaps(int a, int b, int length) =>
        _units[a].File == _units[b].File && Copy(a, length).Span.OverlapsWith(Copy(b, length).Span);

    // `Assert.Equal(a.X, b.X)` eight times: a loop or a table is the fix, not an extracted method.
    private bool IsPeriodic(int start, int length) =>
        Enumerable.Range(1, length / MinimumRepeats).Any(period =>
            Enumerable.Range(start, length - period).All(k => _units[k].Hash == _units[k + period].Hash));

    // Each identifier in one copy maps to exactly one in the other, both ways, so an extracted
    // method's parameters can stand for the differences.
    private bool RenamesConsistently(int a, int b, int length)
    {
        var (first, second) = (_files[_units[a].File], _files[_units[b].File]);
        var forward = new Dictionary<int, int>();
        var backward = new Dictionary<int, int>();
        var offset = _units[b].First - _units[a].First;
        for (var i = _units[a].First; i <= _units[a + length - 1].Last; i++)
        {
            var j = i + offset;
            if (first.Raw[i] == 0)
            {
                continue;
            }

            if (forward.TryGetValue(first.Raw[i], out var mapped) && mapped != second.Raw[j]
                || backward.TryGetValue(second.Raw[j], out var back) && back != first.Raw[i])
            {
                return false;
            }

            forward[first.Raw[i]] = second.Raw[j];
            backward[second.Raw[j]] = first.Raw[i];
        }

        return true;
    }

    private int RegionTokens(int start, int length) => _units[start + length - 1].Last - _units[start].First + 1;

    private ulong RegionHash(int start, int length) => _files[_units[start].File].Hash(_units[start].First, _units[start + length - 1].Last);

    // The number of units from `start` whose tokens make up a region of the key's size.
    private int Length(int start, (ulong Hash, int Tokens) key)
    {
        var length = 1;
        while (RegionTokens(start, length) < key.Tokens)
        {
            length++;
        }

        return length;
    }

    private CloneCopy Copy(int start, int length)
    {
        var (first, last) = (_units[start], _units[start + length - 1]);
        var file = _files[first.File].File;
        return new CloneCopy(file.Path, file.Tree, TextSpan.FromBounds(first.Node!.SpanStart, last.Node!.Span.End), first.Node, last.Node);
    }

    // One file's tokens as normalized kinds (`Norm`) and interned identifier names (`Raw`, 0 for
    // other tokens), with prefix hashes so any token range hashes in constant time.
    private sealed class FileTokens
    {
        private readonly ulong[] _prefix;

        private readonly ulong[] _power;

        public FileTokens(SourceSetFile file, int[] norm, int[] raw)
        {
            File = file;
            Norm = norm;
            Raw = raw;
            _prefix = new ulong[norm.Length + 1];
            _power = new ulong[norm.Length + 1];
            for (var i = 0; i <= norm.Length; i++)
            {
                _prefix[i] = i == 0 ? 0 : unchecked((_prefix[i - 1] * Base) + (ulong)norm[i - 1] + 1);
                _power[i] = i == 0 ? 1 : unchecked(_power[i - 1] * Base);
            }
        }

        public SourceSetFile File { get; }

        public int[] Norm { get; }

        public int[] Raw { get; }

        public ulong Hash(int first, int last) => unchecked(_prefix[last + 1] - (_prefix[first] * _power[last - first + 1]));
    }

    // A statement in a sequence, or a sentinel that ends one so no clone crosses a block boundary.
    private readonly record struct Unit(int File, int First, int Last, ulong Hash, int Statements, StatementSyntax? Node)
    {
        public static Unit Sentinel => new(-1, 0, -1, 0, 0, null);

        public bool IsSentinel => File < 0;
    }
}

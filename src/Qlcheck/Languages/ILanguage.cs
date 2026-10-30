using Qlcheck.Checks;
using Qlcheck.Scan;

namespace Qlcheck.Languages;

internal interface ILanguage
{
    string Id { get; }

    // Every Check this Language owns, implemented or not. Ids are unique across Languages.
    IReadOnlyList<ICheck> Checks { get; }

    bool Matches(string path);

    RunResult Execute(
        IReadOnlyList<SourceScan.LoadedSource> files,
        IReadOnlyList<ICheck> checks);
}

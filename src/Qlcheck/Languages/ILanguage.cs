using Qlcheck.Checks;
using Qlcheck.Scan;

namespace Qlcheck.Languages;

internal interface ILanguage
{
    string Id { get; }

    bool Matches(string path);

    RunResult Execute(
        IReadOnlyList<SourceScan.LoadedSource> files,
        IReadOnlyList<ICheck> checks);
}

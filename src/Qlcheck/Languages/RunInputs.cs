using Qlcheck.Scan;

namespace Qlcheck.Languages;

// Repository-level inputs a Language needs beyond the files and Checks: for now the declared
// architecture. A record, so the next repository file does not change ILanguage again.
internal sealed record RunInputs(ArchitectureFiles Architecture)
{
    public static RunInputs None { get; } = new(ArchitectureFiles.None);
}

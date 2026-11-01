namespace Qlcheck.Scan;

internal enum ConstraintKind
{
    // `S… must-not-use T…`: code under S must not reference a type under T, except inside S.
    MustNotUse,

    // `T… only-used-by S…`: a type under T may be referenced only from code under S, or inside T.
    OnlyUsedBy,

    // `no-cycles P.*`: the direct children of P, each with everything below it, form no cycle.
    NoCycles,
}

global using Qlcheck;
global using Qlcheck.Languages.CSharp;
global using Qlcheck.Languages.CSharp.Checks.Coverage;
global using Qlcheck.Run;
global using Qlcheck.Scan;

namespace Qlcheck.Tests;

internal static class GlobalUsingAnchor
{
    internal static readonly Type[] Types =
    [
        typeof(QlcheckApp),
        typeof(CheckDiscovery),
        typeof(IgnorePatterns),
    ];
}

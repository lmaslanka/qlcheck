using System.Security.Cryptography;
using IOFile = System.IO.File;
using System.Text;

namespace Qlcheck.Languages.CSharp.Checks.Coverage;

internal static class CoverageCache
{
    private const string FolderName = "qlcheck-coverage";

    private const string InfoSuffix = ".info";

    public static bool TryRead(string testDll, string productDll, string include, out string lcov)
    {
        lcov = string.Empty;
        var path = PathFor(testDll, productDll, include);
        if (!IOFile.Exists(path))
        {
            return false;
        }

        lcov = IOFile.ReadAllText(path);
        return lcov.Length > 0;
    }

    public static void Write(string testDll, string productDll, string include, string lcov)
    {
        var path = PathFor(testDll, productDll, include);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        IOFile.WriteAllText(path, lcov);
    }

    private static string PathFor(string testDll, string productDll, string include)
    {
        var key = string.Join(
            '\n',
            testDll,
            IOFile.GetLastWriteTimeUtc(testDll).Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            productDll,
            IOFile.GetLastWriteTimeUtc(productDll).Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            include);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(Path.GetTempPath(), FolderName, $"{hash}{InfoSuffix}");
    }
}

using IOFile = System.IO.File;

namespace Qlcheck.Languages.CSharp.Checks.Coverage;

internal static class CoverletTool
{
    private const string FolderName = "coverlet";

    private const string ConsoleDll = "coverlet.console.dll";

    public static string Dll()
    {
        var path = Path.Combine(AppContext.BaseDirectory, FolderName, ConsoleDll);
        if (!IOFile.Exists(path))
        {
            throw new InvalidOperationException($"coverlet.console was not found at {path}.");
        }

        return path;
    }
}

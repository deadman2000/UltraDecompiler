namespace Tools.Commands;

internal static class Utils
{
    public static string ResolveIncludeDirectory(string? incDir)
    {
        if (!string.IsNullOrWhiteSpace(incDir))
        {
            return Path.GetFullPath(incDir);
        }

        return Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "QuickC", "INCLUDE"));
    }

    public static string ResolveLibraryDirectory(string? libDir)
    {
        if (!string.IsNullOrWhiteSpace(libDir))
        {
            return Path.GetFullPath(libDir);
        }

        return Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "QuickC"));
    }

    public static string ResolveOutputDirectory(string exePath, string? outputDir)
    {
        if (!string.IsNullOrWhiteSpace(outputDir))
        {
            return Path.GetFullPath(outputDir);
        }

        return Path.GetDirectoryName(Path.GetFullPath(exePath)) ?? ".";
    }

    public static void ClearDirectory(string path)
    {
        var di = new DirectoryInfo(path);
        di.Create();

        foreach (FileInfo file in di.GetFiles())
        {
            file.Delete();
        }
        foreach (DirectoryInfo dir in di.GetDirectories())
        {
            dir.Delete(true);
        }
    }
}

namespace LibParserTests;

/// <summary>Пути к эталонным .LIB из комплекта QuickC.</summary>
internal static class QuickCLibAssets
{
    private static readonly string QuickCRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "QuickC"));

    public static string PathOf(string fileName) =>
        Path.Combine(QuickCRoot, fileName);

    public static bool Exists(string fileName) =>
        File.Exists(PathOf(fileName));

    /// <summary>Имена файлов <c>*.LIB</c> в корне <see cref="QuickCRoot"/>.</summary>
    public static IEnumerable<string> EnumerateLibFileNames()
    {
        if (!Directory.Exists(QuickCRoot))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(QuickCRoot, "*.LIB").OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
        {
            yield return Path.GetFileName(path);
        }
    }
}

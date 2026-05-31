namespace DecompilerTests.Decompilation;

/// <summary>Пути к эталонным артефактам QuickC (EXE, LIB).</summary>
internal static class QuickCTestAssets
{
    private static readonly string QuickCRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "QuickC"));

    public static string ProgramsPathOf(string fileName) =>
        Path.Combine(QuickCRoot, "PROGRAMS", fileName);

    public static string LibDirectory => QuickCRoot;

    public static string IncludeDirectory => Path.Combine(QuickCRoot, "INCLUDE");
}

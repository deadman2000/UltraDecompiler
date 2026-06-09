namespace TestSupport;

/// <summary>Пути к эталонным артефактам QuickC (EXE, LIB, исходники).</summary>
public static class QuickCTestAssets
{
    private static readonly string QuickCRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "QuickC"));

    /// <summary>Корень каталога QuickC в репозитории.</summary>
    public static string QuickCRootDirectory => QuickCRoot;

    /// <summary>Каталог с исходниками эталонных программ (<c>*.c</c> верхнего уровня).</summary>
    public static string ProgramsDirectory => Path.Combine(QuickCRoot, "PROGRAMS");

    /// <summary>Каталог кэша собранных EXE для тестов (<see cref="ExeProvider"/>).</summary>
    public static string BuiltExesDirectory => Path.Combine(QuickCRoot, "BUILT");

    /// <summary>Корень изолированных рабочих каталогов round-trip тестов (короткое имя <c>RT</c> для DOS).</summary>
    public static string RoundTripWorkRoot => Path.Combine(QuickCRoot, "RT");

    public static string ProgramsPathOf(string fileName) =>
        Path.Combine(ProgramsDirectory, fileName);

    public static string LibDirectory => QuickCRoot;

    public static string LibPathOf(string fileName) =>
        Path.Combine(LibDirectory, fileName);

    public static string IncludeDirectory => Path.Combine(QuickCRoot, "INCLUDE");

    public static string DosBoxConfigPath => Path.Combine(QuickCRoot, "dosbox.conf");
}

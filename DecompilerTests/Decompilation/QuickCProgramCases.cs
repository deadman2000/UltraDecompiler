namespace DecompilerTests.Decompilation;

/// <summary>Динамический набор исходников <c>QuickC/PROGRAMS/*.c</c> для параметризованных тестов.</summary>
internal static class QuickCProgramCases
{
    /// <summary>Имена <c>*.c</c> в <see cref="QuickCTestAssets.ProgramsDirectory"/> (без подкаталогов).</summary>
    public static IEnumerable<object[]> SourceFileMemberData()
    {
        if (!Directory.Exists(QuickCTestAssets.ProgramsDirectory))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(QuickCTestAssets.ProgramsDirectory, "*.c"))
        {
            yield return [Path.GetFileName(path)];
        }
    }
}

namespace DecompilerTests.Decompilation;

/// <summary>Динамический набор исходников <c>QuickC/PROGRAMS/*.c</c> для параметризованных тестов.</summary>
internal static class QuickCProgramCases
{
    private static readonly Lazy<HashSet<string>> KnownRoundTripFailures = new(LoadKnownRoundTripFailures);

    /// <summary>Имена <c>*.c</c> в <see cref="QuickCTestAssets.ProgramsDirectory"/> (без подкаталогов).</summary>
    public static IEnumerable<object[]> SourceFileMemberData()
    {
        if (!Directory.Exists(QuickCTestAssets.ProgramsDirectory))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(QuickCTestAssets.ProgramsDirectory, "*.c"))
        {
            var fileName = Path.GetFileName(path);
            if (KnownRoundTripFailures.Value.Contains(fileName))
            {
                continue;
            }

            yield return [fileName];
        }
    }

    private static HashSet<string> LoadKnownRoundTripFailures()
    {
        var manifestPath = Path.Combine(QuickCTestAssets.ProgramsDirectory, "roundtrip_xfail.txt");
        if (!File.Exists(manifestPath))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return File.ReadAllLines(manifestPath)
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0 && !line.StartsWith('#'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}

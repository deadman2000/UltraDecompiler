namespace TestSupport;

/// <summary>Пути к DOSBox-X и проверка доступности QuickC toolchain для интеграционных тестов.</summary>
public static class DosBoxQuickCAssets
{
    private static readonly Lazy<string?> ResolvedDosBoxExecutableLazy = new(ResolveDosBoxExecutable);

    /// <summary>Разрешённый путь к <c>dosbox-x.exe</c> или <see langword="null"/>.</summary>
    public static string? ResolvedDosBoxExecutable => ResolvedDosBoxExecutableLazy.Value;

    /// <summary><see langword="true"/>, если найден исполняемый файл DOSBox-X.</summary>
    public static bool IsDosBoxAvailable => ResolvedDosBoxExecutable is not null;

    private static string? ResolveDosBoxExecutable()
    {
        var fromEnv = Environment.GetEnvironmentVariable("DOSBOX_X_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
        {
            return fromEnv;
        }

        foreach (var fileName in new[] { "dosbox-x.exe", "dosbox-x" })
        {
            var fromPath = ResolveFromPath(fileName);
            if (fromPath is not null)
            {
                return fromPath;
            }
        }

        return null;
    }

    private static string? ResolveFromPath(string fileName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
        {
            return null;
        }

        foreach (var directory in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim(), fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}

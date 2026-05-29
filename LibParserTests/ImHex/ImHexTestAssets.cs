namespace LibParserTests.ImHex;

/// <summary>Пути к ImHex и шаблону <c>omf_lib.hexpat</c> для интеграционных тестов.</summary>
internal static class ImHexTestAssets
{
    private const string DefaultImHexDirectory = @"C:\Program Files\ImHex\";

    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    /// <summary>Шаблон Pattern Language для OMF .LIB.</summary>
    public static string OmfLibPatternPath =>
        Path.Combine(RepositoryRoot, "LibParser", "ImHex", "omf_lib.hexpat");

    /// <summary>Путь к <c>imhex.exe</c> (переопределяется переменной окружения <c>IMHEX_PATH</c>).</summary>
    public static string ImHexExecutable
    {
        get
        {
            var fromEnv = Environment.GetEnvironmentVariable("IMHEX_PATH");
            if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
            {
                return fromEnv;
            }

            return Path.Combine(DefaultImHexDirectory, "imhex.exe");
        }
    }

    public static bool IsImHexAvailable => File.Exists(ImHexExecutable);

    public static bool IsPatternAvailable => File.Exists(OmfLibPatternPath);
}

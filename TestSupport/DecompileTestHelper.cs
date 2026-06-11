using UltraDecompiler.Decompilation;

namespace TestSupport;

/// <summary>Вспомогательные методы для интеграционных тестов декомпиляции.</summary>
public static class DecompileTestHelper
{
    /// <summary>Путь к единственному сгенерированному .c в каталоге вывода.</summary>
    public static string GetPrimarySourcePath(DecompileResult result) =>
        result.OutputFiles.Single(path => path.EndsWith(".c", StringComparison.OrdinalIgnoreCase));

    /// <summary>Читает содержимое основного сгенерированного .c.</summary>
    public static string ReadPrimarySource(DecompileResult result) =>
        File.ReadAllText(GetPrimarySourcePath(result));
}

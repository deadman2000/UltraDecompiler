using UltraDecompiler.Common;
using UltraDecompiler.Decompilation;
using UltraDecompiler.Ir.Procedures;

namespace TestSupport;

/// <summary>Вспомогательные методы для интеграционных тестов декомпиляции.</summary>
public static class DecompileTestHelper
{
    /// <summary>Имя единственного сгенерированного .c.</summary>
    public static string GetPrimarySourceFileName(DecompileResult result) =>
        result.GeneratedFiles.Single(file => file.FileName.EndsWith(".c", StringComparison.OrdinalIgnoreCase)).FileName;

    /// <summary>Читает содержимое основного сгенерированного .c.</summary>
    public static string ReadPrimarySource(DecompileResult result) =>
        result.GeneratedFiles.Single(file => file.FileName.EndsWith(".c", StringComparison.OrdinalIgnoreCase)).Content;

    /// <summary>Читает содержимое сгенерированного файла по имени.</summary>
    public static string ReadGeneratedFile(DecompileResult result, string fileName) =>
        result.GeneratedFiles.Single(file => string.Equals(file.FileName, fileName, StringComparison.OrdinalIgnoreCase)).Content;

    /// <summary>Читает содержимое первого сгенерированного файла, удовлетворяющего предикату.</summary>
    public static string ReadGeneratedFile(
        DecompileResult result,
        Func<string, bool> fileNamePredicate) =>
        result.GeneratedFiles.Single(file => fileNamePredicate(file.FileName)).Content;

    /// <summary>Содержимое всех сгенерированных .c файлов.</summary>
    public static IEnumerable<string> ReadAllSourceContents(DecompileResult result) =>
        result.GeneratedFiles
            .Where(file => file.FileName.EndsWith(".c", StringComparison.OrdinalIgnoreCase))
            .Select(file => file.Content);

    /// <summary>
    /// Выполняет декомпиляцию EXE-файла примера QuickC с параметрами по умолчанию.
    /// </summary>
    /// <param name="sourceFileName">Имя исходника (например <c>hello.c</c>).</param>
    /// <param name="memoryModel">Модель памяти (по умолчанию Small).</param>
    /// <param name="stackCheck">Включить проверку стека (по умолчанию false).</param>
    /// <param name="optimization">Уровень оптимизации (по умолчанию Disabled).</param>
    /// <param name="libraries">Список библиотек для линковки.</param>
    /// <param name="libraryFileNames">Конкретные .LIB для сопоставления (опционально).</param>
    /// <returns>Результат декомпиляции в памяти.</returns>
    public static DecompileResult DecompileExample(
        string sourceFileName,
        MemoryModel memoryModel = MemoryModel.Small,
        bool stackCheck = false,
        OptimizationLevel optimization = OptimizationLevel.Disabled,
        string[]? libraries = null,
        string[]? libraryFileNames = null)
    {
        var exePath = ExeProvider.Get(sourceFileName, memoryModel, stackCheck, optimization, libraries);
        return DecompileExe(exePath, libraryFileNames);
    }

    /// <summary>
    /// Выполняет декомпиляцию готового EXE-файла в память.
    /// </summary>
    /// <param name="exePath">Путь к EXE-файлу.</param>
    /// <param name="libraryFileNames">Конкретные .LIB для сопоставления (опционально).</param>
    /// <returns>Результат декомпиляции в памяти.</returns>
    public static DecompileResult DecompileExe(
        string exePath,
        string[]? libraryFileNames = null)
    {
        var decompiler = new Decompiler(
            exePath,
            QuickCTestAssets.LibDirectory,
            QuickCTestAssets.IncludeDirectory,
            outputDirectory: null,
            libraryFileNames: libraryFileNames);

        return decompiler.Decompile(outputMode: DecompileOutputMode.InMemory);
    }

    public static IReadOnlyCollection<DisassembledProcedure> GetExampleIR(
        string sourceFileName,
        MemoryModel memoryModel = MemoryModel.Small,
        bool stackCheck = false,
        OptimizationLevel optimization = OptimizationLevel.Disabled,
        string[]? libraries = null)
    {
        var exePath = ExeProvider.Get(sourceFileName, memoryModel, stackCheck, optimization, libraries);
        return GetExampleIRExe(exePath);
    }

    private static IReadOnlyCollection<DisassembledProcedure> GetExampleIRExe(string exePath)
    {
        var decompiler = new Decompiler(
            exePath,
            QuickCTestAssets.LibDirectory,
            QuickCTestAssets.IncludeDirectory,
            null);

        return decompiler.BuildIR();
    }
}

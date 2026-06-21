using UltraDecompiler.Common;
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

    /// <summary>
    /// Выполняет декомпиляцию EXE-файла примера QuickC с параметрами по умолчанию.
    /// </summary>
    /// <param name="sourceFileName">Имя исходника (например <c>hello.c</c>).</param>
    /// <param name="memoryModel">Модель памяти (по умолчанию Small).</param>
    /// <param name="stackCheck">Включить проверку стека (по умолчанию false).</param>
    /// <param name="optimization">Уровень оптимизации (по умолчанию Disabled).</param>
    /// <param name="libraries">Список библиотек для линковки.</param>
    /// <param name="libraryFileNames">Конкретные .LIB для сопоставления (опционально).</param>
    /// <param name="exportGraph">Экспортировать графы CFG в DOT/SVG (по умолчанию false).</param>
    /// <returns>Результат декомпиляции с временным каталогом вывода.</returns>
    public static DecompileResult DecompileExample(
        string sourceFileName,
        MemoryModel memoryModel = MemoryModel.Small,
        bool stackCheck = false,
        OptimizationLevel optimization = OptimizationLevel.Disabled,
        string[]? libraries = null,
        string[]? libraryFileNames = null,
        bool exportGraph = false)
    {
        var exePath = ExeProvider.Get(sourceFileName, memoryModel, stackCheck, optimization, libraries);
        return DecompileExample(
            exePath,
            libraryFileNames,
            exportGraph);
    }

    /// <summary>
    /// Выполняет декомпиляцию готового EXE-файла.
    /// </summary>
    /// <param name="exePath">Путь к EXE-файлу.</param>
    /// <param name="libraryFileNames">Конкретные .LIB для сопоставления (опционально).</param>
    /// <param name="exportGraph">Экспортировать графы CFG в DOT/SVG (по умолчанию false).</param>
    /// <returns>Результат декомпиляции с временным каталогу вывода.</returns>
    private static DecompileResult DecompileExample(
        string exePath,
        string[]? libraryFileNames = null,
        bool exportGraph = false)
    {
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "UltraDecompilerTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(outputDirectory);

        try
        {
            var decompiler = new Decompiler(
                exePath,
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory,
                libraryFileNames: libraryFileNames);

            return decompiler.Decompile(exportGraph);
        }
        catch
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
            throw;
        }
    }

    /// <summary>
    /// Выполняет декомпиляцию и возвращает путь к временному каталогу вывода для последующей очистки.
    /// </summary>
    /// <param name="sourceFileName">Имя исходника (например <c>hello.c</c>).</param>
    /// <param name="outputDirectory">Созданный каталог вывода (out-параметр).</param>
    /// <param name="memoryModel">Модель памяти (по умолчанию Small).</param>
    /// <param name="stackCheck">Включить проверку стека (по умолчанию false).</param>
    /// <param name="optimization">Уровень оптимизации (по умолчанию Disabled).</param>
    /// <param name="libraries">Список библиотек для линковки.</param>
    /// <param name="libraryFileNames">Конкретные .LIB для сопоставления (опционально).</param>
    /// <param name="exportGraph">Экспортировать графы CFG в DOT/SVG (по умолчанию false).</param>
    /// <returns>Результат декомпиляции.</returns>
    public static DecompileResult DecompileExampleWithCleanup(
        string sourceFileName,
        out string outputDirectory,
        MemoryModel memoryModel = MemoryModel.Small,
        bool stackCheck = false,
        OptimizationLevel optimization = OptimizationLevel.Disabled,
        string[]? libraries = null,
        string[]? libraryFileNames = null,
        bool exportGraph = false)
    {
        outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "UltraDecompilerTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(outputDirectory);

        var exePath = ExeProvider.Get(sourceFileName, memoryModel, stackCheck, optimization, libraries);

        var decompiler = new Decompiler(
            exePath,
            QuickCTestAssets.LibDirectory,
            QuickCTestAssets.IncludeDirectory,
            outputDirectory,
            libraryFileNames: libraryFileNames);

        return decompiler.Decompile(exportGraph);
    }
}

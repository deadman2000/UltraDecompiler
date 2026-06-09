using UltraDecompiler.Compilation;

namespace DecompilerTests;

internal static class ExeProvider
{
    /// <summary>
    /// Возвращает путь к exe-файлу примера, собранного с заданными параметрами.
    /// </summary>
    /// <param name="fileName">Имя файла примера.</param>
    public static string Get(
        string fileName,
        MemoryModel memoryModel = MemoryModel.Compact,
        bool stackCheck = true,
        OptimizationLevel optimization = OptimizationLevel.Enabled)
    {
        throw new NotImplementedException();
    }
}

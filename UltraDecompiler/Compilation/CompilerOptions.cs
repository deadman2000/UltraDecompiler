namespace UltraDecompiler.Compilation;

/// <summary>
/// Восстановленные параметры компиляции для декомпилируемой программы.
/// </summary>
public sealed record CompilerOptions
{
    /// <summary>Модель памяти, восстановленная по CRT-библиотеке.</summary>
    public MemoryModel MemoryModel { get; init; }

    /// <summary>
    /// <see langword="true"/>, если включена проверка стека.
    /// </summary>
    public bool StackCheckingEnabled { get; init; }

    public override string ToString()
    {
        var memoryLine = MemoryModel == MemoryModel.Unknown
            ? "Модель памяти QuickC: не определена."
            : $"Модель памяти QuickC: {MemoryModelDetector.GetDisplayName(MemoryModel)} ({MemoryModelDetector.GetCompilerFlag(MemoryModel)}).";

        var stackLine = StackCheckingEnabled
            ? "Проверка стека QuickC: включена (по умолчанию, без /Gs)."
            : "Проверка стека QuickC: отключена (флаг /Gs).";

        return $"{memoryLine}{Environment.NewLine}{stackLine}";
    }

    public string GetQuickCCompilerFlags()
    {
        var flags = new List<string>();

        var memoryFlag = MemoryModelDetector.GetCompilerFlag(MemoryModel);
        if (!string.IsNullOrEmpty(memoryFlag))
        {
            flags.Add(memoryFlag);
        }

        if (!StackCheckingEnabled)
        {
            flags.Add("/Gs");
        }

        return string.Join(' ', flags);
    }
}

namespace UltraDecompiler.Decompilation;

/// <summary>
/// Восстановленные параметры компиляции для декомпилируемой программы.
/// </summary>
public sealed record CompilerOptions
{
    /// <summary>
    /// <see langword="true"/>, если включена проверка стека.
    /// </summary>
    public bool StackCheckingEnabled { get; init; }
    
    public override string ToString()
    {
        return StackCheckingEnabled
            ? "Проверка стека QuickC: включена (по умолчанию, без /Gs)."
            : "Проверка стека QuickC: отключена (флаг /Gs).";
    }
}

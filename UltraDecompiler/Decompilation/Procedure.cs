namespace UltraDecompiler.Decompilation;

/// <summary>
/// Процедура или метод
/// </summary>
public class Procedure
{
    /// <summary>
    /// Имя
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Параметры (для декомпилируемой функции или известной сигнатуры).
    /// </summary>
    public IReadOnlyList<FunctionParameter> Parameters { get; init; } = [];

    // TODO возвращаемый тип
}

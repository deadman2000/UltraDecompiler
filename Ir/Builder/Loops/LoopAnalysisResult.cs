namespace UltraDecompiler.Ir.Builder.Loops;

/// <summary>
/// Результат анализа цикла: тип, параметры и тело.
/// </summary>
public sealed record LoopAnalysisResult
{
    /// <summary>
    /// Тип распознанного цикла.
    /// </summary>
    public required LoopType LoopType { get; init; }

    /// <summary>
    /// Операция инициализации (для for).
    /// </summary>
    public Operation? Init { get; init; }

    /// <summary>
    /// Условие продолжения цикла.
    /// </summary>
    public required Expr Condition { get; init; }

    /// <summary>
    /// Операция обновления счётчика (для for).
    /// </summary>
    public Operation? Iteration { get; init; }

    /// <summary>
    /// Тело цикла (операции внутри).
    /// </summary>
    public required IReadOnlyList<Operation> Body { get; init; }

    /// <summary>
    /// Блок выхода из цикла (для сбора кода после цикла).
    /// </summary>
    public ExprBlock? ExitBlock { get; init; }

    /// <summary>
    /// Создаёт IR-операцию цикла на основе результата анализа.
    /// </summary>
    public Operation ToOperation()
    {
        return LoopType switch
        {
            LoopType.For => new ForOperation(Init, Condition, Iteration, Body),
            LoopType.While => new WhileOperation(Condition, Body),
            LoopType.DoWhile => new DoWhileOperation(Condition, Body),
            _ => throw new InvalidOperationException($"Неизвестный тип цикла: {LoopType}")
        };
    }
}

/// <summary>
/// Тип распознанного цикла.
/// </summary>
public enum LoopType
{
    /// <summary>
    /// Цикл for с инициализацией, условием и обновлением.
    /// </summary>
    For,

    /// <summary>
    /// Цикл while с проверкой условия в начале.
    /// </summary>
    While,

    /// <summary>
    /// Цикл do-while с проверкой условия в конце.
    /// </summary>
    DoWhile
}

/// <summary>
/// Раскладка цикла: вход в тело, выход, условие продолжения.
/// </summary>
internal readonly record struct LoopLayout(
    ExprBlock BodyStart,
    ExprBlock? ExitBlock,
    Expr ContinueCondition);

namespace UltraDecompiler.Ir.Calls;

/// <summary>
/// Состояние вызова на момент CALL.
/// </summary>
public sealed record CallState
{
    /// <summary>
    /// Смещение цели near CALL (для прямых вызовов E8 rel16).
    /// </summary>
    public int TargetOffset { get; init; }

    /// <summary>
    /// Снимок стека на момент вызова (вершина стека — индекс 0).
    /// </summary>
    public IReadOnlyList<Expr>? CallSiteStack { get; init; }

    /// <summary>
    /// Снимок состояния регистров на момент вызова.
    /// </summary>
    public RegisterExpressions? CallSiteRegisters { get; init; }

    /// <summary>
    /// Выражения, явно подготовленные через последовательность PUSH непосредственно перед CALL
    /// (в текущем базовом блоке). Предпочтительный источник stack-аргументов для этого вызова.
    /// </summary>
    public IReadOnlyList<Expr>? CallSitePushArgs { get; init; }
}

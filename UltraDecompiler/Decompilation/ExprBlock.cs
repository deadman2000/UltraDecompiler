using UltraDecompiler.Graph;

namespace UltraDecompiler.Decompilation;

/// <summary>
/// Блок выражений
/// </summary>
public class ExprBlock(BasicBlock basicBlock)
{
    /// <summary>
    /// Связанный ассемблерный блок
    /// </summary>
    public BasicBlock BasicBlock { get; } = basicBlock;

    /// <summary>
    /// Операции в порядке выполнения.
    /// </summary>
    public List<Operation> Operations { get; } = [];

    /// <summary>
    /// Следующий блок
    /// </summary>
    public ExprBlock? Next { get; set; }

    /// <summary>
    /// Следующий блок, если выполнилось условие (выражение вернуло true)
    /// </summary>
    public ExprBlock? ConditionalBlock { get; set; }

    /// <summary>
    /// Условие перехода в ConditionalBlock
    /// </summary>
    public Expr? Condition { get; set; }

    /// <summary>
    /// Начальное состояние регистров
    /// </summary>
    public RegisterExpressions InitRegisters { get; set; }

    /// <summary>
    /// Конечное состояние регистров
    /// </summary>
    public RegisterExpressions EndRegisters { get; set; }
}

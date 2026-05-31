using UltraDecompiler.Graph;

namespace UltraDecompiler.Decompilation;

/// <summary>
/// Блок выражений
/// </summary>
public class ExprBlock(BasicBlock basicBlock)
{
    /// <summary>
    /// Общее хранилище переменных
    /// </summary>
    public required VariableStorage Variables { get; init; }

    /// <summary>
    /// Связанный ассемблерный блок
    /// </summary>
    public BasicBlock BasicBlock { get; } = basicBlock;

    /// <summary>
    /// Операции в порядке выполнения
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

    /// <summary>
    /// Начальное состояние стека
    /// </summary>
    public required IReadOnlyList<Expr> InitStack { get; init; }

    /// <summary>
    /// Конеченое состояние стека
    /// </summary>
    public Stack<Expr> EndStack { get; set; } = [];

    /// <summary>Дизассемблированные процедуры образа (имена и сигнатуры для CALL).</summary>
    public ProcedureStorage? Procedures { get; set; }
}

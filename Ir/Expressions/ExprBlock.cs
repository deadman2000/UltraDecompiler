using UltraDecompiler.Ir.Variables;

namespace UltraDecompiler.Ir.Expressions;

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
    /// Операнды последнего CMP в этом блоке (для вывода знаковости по условным переходам).
    /// </summary>
    public (Expr Left, Expr Right)? LastComparisonOperands { get; set; }

    /// <summary>Мнемоника инструкции, выполненной непосредственно перед текущей.</summary>
    public Mnemonic? PreviousMnemonic { get; set; }

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
    /// Начальное состояние стека
    /// </summary>
    public required IReadOnlyList<Expr> InitStack { get; init; }

    /// <summary>
    /// Конеченое состояние стека
    /// </summary>
    public Stack<Expr> EndStack { get; set; } = [];

    /// <summary>
    /// Выражения, положенные на стек инструкциями PUSH (по смещению инструкции).
    /// Нужно для восстановления аргументов CALL: <c>GetExpression</c> на конце блока
    /// для PUSH reg даёт текущее значение регистра, а не то, что было в момент push.
    /// </summary>
    public Dictionary<int, Expr> PushExprsByOffset { get; } = [];

    /// <summary>
    /// Хелпер для SetOperation
    /// </summary>
    public void Set(Expr dst, Expr src)
    {
        Operations.Add(new SetOperation(dst, src));
    }

    public void Set(GpRegister16 reg, Expr src) => Set(Variables.Get(reg), src);

    public void Set(GpRegister8 reg, Expr src) => Set(Variables.Get(reg), src);

    public void Set(CpuSegmentRegister reg, Expr src) => Set(Variables.Get(reg), src);
}

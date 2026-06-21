namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Строит высокоуровневые условия для Jcc сразу после CMP/AND/TEST по операндам сравнения.
/// </summary>
internal static class CmpJumpConditions
{
    /// <summary>
    /// Если предыдущая инструкция — CMP/AND/TEST, возвращает сравнение операндов для данного Jcc.
    /// </summary>
    public static bool TryBuild(ExprBlock block, Mnemonic mnemonic, out Expr? condition)
    {
        condition = null;
        if (block.LastComparisonOperands is not { } cmp || block.LastComparison == null)
        {
            return false;
        }

        condition = Map(mnemonic, cmp.Left, cmp.Right, block.LastComparison.Mnemonic);
        return condition is not null;
    }

    private static Expr? Map(Mnemonic mnemonic, Expr left, Expr right, Mnemonic? comparisonMnemonic)
    {
        // Эвристика: AND reg,reg обновляет флаги так же, как CMP reg,0.
        // Поэтому JG после AND SI, SI означает "SI > 0", а не "(SI & SI) > 0".
        // Упрощаем (Math2Expr(And, X, X), 0) → (X, 0).
        // Только для AND — TEST/OR/XOR не являются тождественными операциями.
        var (simplifiedLeft, simplifiedRight) = SimplifySelfAnd(left, right, comparisonMnemonic);

        return mnemonic switch
        {
            // == и != — знаковые и беззнаковые одинаковы
            Mnemonic.JE => new CmpExpr(CmpOperation.Eq, simplifiedLeft, simplifiedRight),
            Mnemonic.JNE => new CmpExpr(CmpOperation.Ne, simplifiedLeft, simplifiedRight),

            // Беззнаковые: JA, JAE, JB, JBE (проверяют только CF и ZF)
            Mnemonic.JB => new CmpExpr(CmpOperation.Ult, simplifiedLeft, simplifiedRight),
            Mnemonic.JAE => new CmpExpr(CmpOperation.Uge, simplifiedLeft, simplifiedRight),
            Mnemonic.JBE => new CmpExpr(CmpOperation.Ule, simplifiedLeft, simplifiedRight),
            Mnemonic.JA => new CmpExpr(CmpOperation.Ugt, simplifiedLeft, simplifiedRight),

            // Знаковые: JL, JLE, JG, JGE (проверяют SF и OF)
            Mnemonic.JL => new CmpExpr(CmpOperation.Lt, simplifiedLeft, simplifiedRight),
            Mnemonic.JGE => new CmpExpr(CmpOperation.Ge, simplifiedLeft, simplifiedRight),
            Mnemonic.JLE => new CmpExpr(CmpOperation.Le, simplifiedLeft, simplifiedRight),
            Mnemonic.JG => new CmpExpr(CmpOperation.Gt, simplifiedLeft, simplifiedRight),

            _ => null,
        };
    }

    /// <summary>
    /// Эвристика: AND reg,reg; JG → reg > 0.
    /// AND SI, SI обновляет флаги так же, как CMP SI, 0.
    /// IR хранит (Math2Expr(And, SI, SI), 0) в LastComparisonOperands.
    /// Возвращаем (SI, 0) для корректного высокоуровневого условия.
    /// Применимо только к AND: OR/XOR/TEST не являются тождественными операциями.
    /// </summary>
    private static (Expr Left, Expr Right) SimplifySelfAnd(Expr left, Expr right, Mnemonic? comparisonMnemonic)
    {
        // Только AND reg,reg — это тождественная операция, обновляющая флаги как CMP reg,0
        if (comparisonMnemonic != Mnemonic.AND)
        {
            return (left, right);
        }

        if (left is not Math2Expr { Operation: Math2Operation.And } andExpr)
        {
            return (left, right);
        }

        if (!IsZero(right))
        {
            return (left, right);
        }

        // Проверяем, что оба операнда AND — одна и та же переменная
        if (!IsSameVariable(andExpr.First, andExpr.Second))
        {
            return (left, right);
        }

        return (andExpr.First, ConstExpr.Zero);
    }

    private static bool IsZero(Expr expr) => expr is ConstExpr { Value: 0 };

    private static bool IsSameVariable(Expr a, Expr b) =>
        (a, b) switch
        {
            (VariableExpr { Var: var va }, VariableExpr { Var: var vb }) => ReferenceEquals(va, vb),
            _ => false,
        };
}

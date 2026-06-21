namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты последовательностей AND/TEST + Jcc, где логическая инструкция обновляет флаги
/// и Jcc строит условие через CmpJumpConditions с эвристикой упрощения AND reg,reg.
/// </summary>
public class LogicalJumpTests : BaseTests
{
    #region AND reg, reg + Jcc — IR-условие упрощается

    [Fact]
    public void And_Si_Si_Jg_ConditionIsSignedGreaterThan()
    {
        // AND SI, SI → LastComparisonOperands = (SI & SI, 0)
        // JG → CmpJumpConditions упрощает до CmpExpr(Gt, SI, 0)
        var expr = BuildExpressionsRaw("""
            23 F6          ; AND SI, SI
            7F 00          ; JG +0 (target)
            """);

        var block = expr.Blocks[0];
        Assert.NotNull(block.Condition);

        // Условие должно быть CmpExpr с Operation.Gt (signed greater)
        var cmp = Assert.IsType<CmpExpr>(block.Condition);
        Assert.Equal(CmpOperation.Gt, cmp.Operation);

        // Эвристика упростила Math2Expr(And, SI, SI) → SI
        Assert.IsType<VariableExpr>(cmp.Left);
        Assert.Equal(0, ((ConstExpr)cmp.Right).Value);
    }

    [Fact]
    public void And_Si_Si_Jge_ConditionIsSignedGreaterEqual()
    {
        var expr = BuildExpressionsRaw("""
            23 F6          ; AND SI, SI
            7D 00          ; JGE +0
            """);

        var block = expr.Blocks[0];
        var cmp = Assert.IsType<CmpExpr>(block.Condition);
        Assert.Equal(CmpOperation.Ge, cmp.Operation);
        Assert.IsType<VariableExpr>(cmp.Left);
        Assert.Equal(0, ((ConstExpr)cmp.Right).Value);
    }

    #endregion

    #region TEST reg, imm + Jcc

    [Fact]
    public void Test_Ax_1_Je_ConditionIsEquality()
    {
        // TEST AX, AX → LastComparisonOperands = (AX & AX, 0)
        // JE → CmpExpr(Eq, AX & AX, 0) — НЕ упрощаем (только AND)
        var expr = BuildExpressionsRaw("""
            85 C0          ; TEST AX, AX
            74 00          ; JE +0
            """);

        var block = expr.Blocks[0];
        var cmp = Assert.IsType<CmpExpr>(block.Condition);
        Assert.Equal(CmpOperation.Eq, cmp.Operation);
        // Left — результат TEST (AX & AX), Right — 0
        Assert.IsType<Math2Expr>(cmp.Left);
        Assert.Equal(0, ((ConstExpr)cmp.Right).Value);
    }

    [Fact]
    public void Test_Ax_0FFh_Jne_ConditionIsNotEqual()
    {
        // TEST AX, 0FFh (16-bit immediate)
        var expr = BuildExpressionsRaw("""
            81 F0 FF 00    ; TEST AX, 0FFh (16-bit)
            75 00          ; JNE +0
            """);

        var block = expr.Blocks[0];
        var cmp = Assert.IsType<CmpExpr>(block.Condition);
        Assert.Equal(CmpOperation.Ne, cmp.Operation);
        Assert.IsType<Math2Expr>(cmp.Left);
        Assert.Equal(0, ((ConstExpr)cmp.Right).Value);
    }

    #endregion

    #region OR и XOR + Jcc — НЕ упрощаются (только AND регрессия)

    [Fact]
    public void Or_Ax_Ax_Je_ConditionIsNotSimplified()
    {
        var expr = BuildExpressionsRaw("""
            0B C0          ; OR AX, AX
            74 00          ; JE +0
            """);

        var block = expr.Blocks[0];
        var cmp = Assert.IsType<CmpExpr>(block.Condition);
        Assert.Equal(CmpOperation.Eq, cmp.Operation);
        // OR не упрощается — Math2Expr(Or, AX, AX)
        Assert.IsType<Math2Expr>(cmp.Left);
        Assert.Equal(0, ((ConstExpr)cmp.Right).Value);
    }

    [Fact]
    public void Xor_Ax_Ax_Je_ConditionIsNotSimplified()
    {
        var expr = BuildExpressionsRaw("""
            31 C0          ; XOR AX, AX
            74 00          ; JE +0
            """);

        var block = expr.Blocks[0];
        var cmp = Assert.IsType<CmpExpr>(block.Condition);
        Assert.Equal(CmpOperation.Eq, cmp.Operation);
        // XOR не упрощается — Math2Expr(Xor, AX, AX)
        Assert.IsType<Math2Expr>(cmp.Left);
        Assert.Equal(0, ((ConstExpr)cmp.Right).Value);
    }

    #endregion

    #region LastComparisonOperands

    [Fact]
    public void And_SetsLastComparisonOperands()
    {
        var expr = BuildExpressionsRaw("23 F6"); // AND SI, SI

        var block = expr.Blocks[0];
        Assert.NotNull(block.LastComparisonOperands);
        Assert.NotNull(block.LastComparisonOperands.Value.Left);
    }

    [Fact]
    public void Test_SetsLastComparisonOperands()
    {
        var expr = BuildExpressionsRaw("83 F0 01"); // TEST AX, 1

        var block = expr.Blocks[0];
        Assert.NotNull(block.LastComparisonOperands);
    }

    #endregion

    #region Разные регистры — не упрощаются

    [Fact]
    public void And_Si_Di_Jg_ConditionIsNotSimplified()
    {
        // AND SI, DI — разные регистры, не упрощаем
        var expr = BuildExpressionsRaw("""
            23 F7          ; AND SI, DI
            7F 00          ; JG +0
            """);

        var block = expr.Blocks[0];
        var cmp = Assert.IsType<CmpExpr>(block.Condition);
        Assert.Equal(CmpOperation.Gt, cmp.Operation);
        // Не упрощено — Math2Expr(And, SI, DI)
        Assert.IsType<Math2Expr>(cmp.Left);
        Assert.Equal(0, ((ConstExpr)cmp.Right).Value);
    }

    #endregion

    #region AND reg, reg — не генерирует лишних SetOperation

    [Fact]
    public void And_Si_Si_DoesNotProduceExtraSetOperation()
    {
        // AND SI, SI — тождественная операция, не должна генерировать regSI = regSI & regSI
        var expr = BuildExpressionsRaw("23 F6"); // AND SI, SI

        var block = expr.Blocks[0];

        // Проверим, что нет SetOperation, записывающей результат AND в регистр
        var unwantedSets = block.Operations
            .OfType<SetOperation>()
            .Where(op => op.Src is Math2Expr { Operation: Math2Operation.And } andExpr
                && IsSameVariable(op.Dst, andExpr.First)
                && IsSameVariable(op.Dst, andExpr.Second))
            .ToList();

        Assert.Empty(unwantedSets);
    }

    [Fact]
    public void And_Si_Di_DoesProduceSetOperation()
    {
        // AND SI, DI — не тождественная операция, должна генерировать regSI = regSI & regDI
        var expr = BuildExpressionsRaw("23 F7"); // AND SI, DI

        var block = expr.Blocks[0];

        var andSets = block.Operations
            .OfType<SetOperation>()
            .Where(op => op.Src is Math2Expr { Operation: Math2Operation.And })
            .ToList();

        Assert.Single(andSets);
    }

    private static bool IsSameVariable(Expr target, Expr expr)
    {
        return expr switch
        {
            VariableExpr { Var: var va } => target is VariableExpr { Var: var tb } && ReferenceEquals(tb, va),
            _ => false,
        };
    }

    #endregion
}

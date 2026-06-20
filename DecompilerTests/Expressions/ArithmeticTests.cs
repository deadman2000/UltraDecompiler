namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты арифметических инструкций ADD, SUB, ADC, SBB.
/// Проверяют корректное создание Math2Expr и обновление флагов.
/// </summary>
public class ArithmeticTests : BaseTests
{
    #region ADD

    [Fact]
    public void Add_Ax_Immediate16_ProducesMath2Expr()
    {
        // ADD AX, 1234h
        var expr = BuildExpressions("05 34 12");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        Assert.NotEmpty(sets);

        // Первая операция — установка AX
        var set = sets.First(op => ReferenceEquals(op.Dst, expr.Variables.AX));

        // Значение — AX + 0x1234
        var addExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Add, addExpr.Operation);
        Assert.Equal(expr.Variables.AX, addExpr.First);
        Assert.Equal(0x1234, ((ConstExpr)addExpr.Second).Value);
    }

    [Fact]
    public void Add_Ax_Bx_ProducesMath2Expr()
    {
        // ADD AX, BX
        var expr = BuildExpressions("03 C3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => ReferenceEquals(op.Dst, expr.Variables.AX));

        var addExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Add, addExpr.Operation);
        Assert.Equal(expr.Variables.AX, addExpr.First);
        Assert.Equal(expr.Variables.BX, addExpr.Second);
    }

    [Fact]
    public void Add_Al_Immediate8_ProducesMath2Expr()
    {
        // ADD AL, 55h
        var expr = BuildExpressions("04 55");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => ReferenceEquals(op.Dst, expr.Variables.AX));

        // Для 8-битных регистров — сложное выражение с маской 0xFF
        Assert.IsType<Math2Expr>(set.Src);
    }

    #endregion

    #region SUB

    [Fact]
    public void Sub_Ax_Immediate16_ProducesMath2Expr()
    {
        // SUB AX, 1234h
        var expr = BuildExpressions("2D 34 12");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => ReferenceEquals(op.Dst, expr.Variables.AX));

        var subExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Sub, subExpr.Operation);
        Assert.Equal(expr.Variables.AX, subExpr.First);
        Assert.Equal(0x1234, ((ConstExpr)subExpr.Second).Value);
    }

    [Fact]
    public void Sub_Ax_Bx_ProducesMath2Expr()
    {
        // SUB AX, BX
        var expr = BuildExpressions("2B C3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => ReferenceEquals(op.Dst, expr.Variables.AX));

        var subExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Sub, subExpr.Operation);
        Assert.Equal(expr.Variables.AX, subExpr.First);
        Assert.Equal(expr.Variables.BX, subExpr.Second);
    }

    #endregion

    #region ADC

    [Fact]
    public void Adc_Ax_Immediate16_ProducesMath2ExprWithCarry()
    {
        // ADC AX, 1234h
        var expr = BuildExpressions("15 34 12");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => ReferenceEquals(op.Dst, expr.Variables.AX));

        // ADC: AX = AX + 0x1234 + CF
        var addExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Add, addExpr.Operation);
        // Выражение должно содержать CF
        var exprStr = set.Src.ToString();
        Assert.Contains("regCF", exprStr);
    }

    [Fact]
    public void Adc_Ax_Bx_ProducesMath2ExprWithCarry()
    {
        // ADC AX, BX
        var expr = BuildExpressions("13 C3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => ReferenceEquals(op.Dst, expr.Variables.AX));

        var exprStr = set.Src.ToString();
        Assert.Contains("regCF", exprStr);
    }

    #endregion

    #region SBB

    [Fact]
    public void Sbb_Ax_Immediate16_ProducesMath2ExprWithCarry()
    {
        // SBB AX, 1234h
        var expr = BuildExpressions("1D 34 12");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => ReferenceEquals(op.Dst, expr.Variables.AX));

        // SBB: AX = AX - 0x1234 - CF
        var subExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Sub, subExpr.Operation);
        // Выражение должно содержать CF
        var exprStr = set.Src.ToString();
        Assert.Contains("regCF", exprStr);
    }

    [Fact]
    public void Sbb_Ax_Bx_ProducesMath2ExprWithCarry()
    {
        // SBB AX, BX
        var expr = BuildExpressions("1B C3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => ReferenceEquals(op.Dst, expr.Variables.AX));

        var exprStr = set.Src.ToString();
        Assert.Contains("regCF", exprStr);
    }

    #endregion

    #region Флаги

    [Fact]
    public void Add_UpdatesFlags()
    {
        // ADD AX, BX
        var expr = BuildExpressions("03 C3");

        // Должны быть установлены флаги ZF, SF, CF, OF
        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        Assert.NotEmpty(sets);

        // Проверяем, что есть операции установки флагов
        var hasZf = sets.Any(op => ReferenceEquals(op.Dst, expr.Variables.ZF));
        var hasSf = sets.Any(op => ReferenceEquals(op.Dst, expr.Variables.SF));
        var hasCf = sets.Any(op => ReferenceEquals(op.Dst, expr.Variables.CF));
        var hasOf = sets.Any(op => ReferenceEquals(op.Dst, expr.Variables.OF));

        Assert.True(hasZf, "ZF должен быть установлен");
        Assert.True(hasSf, "SF должен быть установлен");
        Assert.True(hasCf, "CF должен быть установлен");
        Assert.True(hasOf, "OF должен быть установлен");
    }

    [Fact]
    public void Sub_UpdatesFlags()
    {
        // SUB AX, BX
        var expr = BuildExpressions("2B C3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();

        var hasZf = sets.Any(op => ReferenceEquals(op.Dst, expr.Variables.ZF));
        var hasSf = sets.Any(op => ReferenceEquals(op.Dst, expr.Variables.SF));
        var hasCf = sets.Any(op => ReferenceEquals(op.Dst, expr.Variables.CF));
        var hasOf = sets.Any(op => ReferenceEquals(op.Dst, expr.Variables.OF));

        Assert.True(hasZf, "ZF должен быть установлен");
        Assert.True(hasSf, "SF должен быть установлен");
        Assert.True(hasCf, "CF должен быть установлен");
        Assert.True(hasOf, "OF должен быть установлен");
    }

    [Fact]
    public void Adc_UpdatesFlags()
    {
        // ADC AX, BX
        var expr = BuildExpressions("13 C3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();

        var hasZf = sets.Any(op => ReferenceEquals(op.Dst, expr.Variables.ZF));
        var hasSf = sets.Any(op => ReferenceEquals(op.Dst, expr.Variables.SF));
        var hasCf = sets.Any(op => ReferenceEquals(op.Dst, expr.Variables.CF));
        var hasOf = sets.Any(op => ReferenceEquals(op.Dst, expr.Variables.OF));

        Assert.True(hasZf, "ZF должен быть установлен");
        Assert.True(hasSf, "SF должен быть установлен");
        Assert.True(hasCf, "CF должен быть установлен");
        Assert.True(hasOf, "OF должен быть установлен");
    }

    [Fact]
    public void Sbb_UpdatesFlags()
    {
        // SBB AX, BX
        var expr = BuildExpressions("1B C3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();

        var hasZf = sets.Any(op => ReferenceEquals(op.Dst, expr.Variables.ZF));
        var hasSf = sets.Any(op => ReferenceEquals(op.Dst, expr.Variables.SF));
        var hasCf = sets.Any(op => ReferenceEquals(op.Dst, expr.Variables.CF));
        var hasOf = sets.Any(op => ReferenceEquals(op.Dst, expr.Variables.OF));

        Assert.True(hasZf, "ZF должен быть установлен");
        Assert.True(hasSf, "SF должен быть установлен");
        Assert.True(hasCf, "CF должен быть установлен");
        Assert.True(hasOf, "OF должен быть установлен");
    }

    #endregion

    #region ADD с памятью

    [Fact]
    public void Add_Memory_Ax_ProducesAddAssignOperation()
    {
        // ADD [BX], AX
        var expr = BuildExpressions("01 07");

        var ops = expr.Blocks[0].Operations;
        Assert.NotEmpty(ops);

        // Должна быть AddAssignOperation или StoreOperation
        var addAssign = ops.OfType<AddAssignOperation>().FirstOrDefault();
        if (addAssign != null)
        {
            Assert.Equal(expr.Variables.BX, addAssign.Target);
            Assert.Equal(expr.Variables.AX, addAssign.Value);
        }
        else
        {
            // Fallback: StoreOperation
            var store = Assert.IsType<StoreOperation>(ops.First());
            Assert.Equal(expr.Variables.BX, store.Address);
        }
    }

    [Fact]
    public void Add_Ax_Memory_ProducesMath2Expr()
    {
        // ADD AX, [BX]
        var expr = BuildExpressions("03 07");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => ReferenceEquals(op.Dst, expr.Variables.AX));

        var addExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Add, addExpr.Operation);
        // Первый операнд — AX
        Assert.Equal(expr.Variables.AX, addExpr.First);
        // Второй операнд — MemExpr
        Assert.IsType<MemExpr>(addExpr.Second);
    }

    #endregion
}

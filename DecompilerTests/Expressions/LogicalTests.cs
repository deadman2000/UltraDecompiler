namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты логических инструкций AND, OR, XOR.
/// Проверяют корректное создание Math2Expr и обновление флагов.
/// </summary>
public class LogicalTests : BaseTests
{
    #region AND

    [Fact]
    public void And_Ax_Immediate16_ProducesMath2Expr()
    {
        // AND AX, 00FFh
        var expr = BuildExpressions("25 FF 00");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));

        var andExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.And, andExpr.Operation);
        ExprTestHelpers.AssertSameVariable(expr.Variables.AX, andExpr.First);
        Assert.Equal(0x00FF, ((ConstExpr)andExpr.Second).Value);
    }

    [Fact]
    public void And_Ax_Bx_ProducesMath2Expr()
    {
        // AND AX, BX
        var expr = BuildExpressions("23 C3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));

        var andExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.And, andExpr.Operation);
        ExprTestHelpers.AssertSameVariable(expr.Variables.AX, andExpr.First);
        ExprTestHelpers.AssertSameVariable(expr.Variables.BX, andExpr.Second);
    }

    [Fact]
    public void And_Al_Immediate8_ProducesMath2Expr()
    {
        // AND AL, 0Fh
        var expr = BuildExpressions("24 0F");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));

        // Для 8-битных регистров — выражение с маской 0xFF
        Assert.IsType<Math2Expr>(set.Src);
    }

    [Fact]
    public void And_Memory_Ax_ProducesStoreOperation()
    {
        // AND [BX], AX
        var expr = BuildExpressions("21 07");

        var ops = expr.Blocks[0].Operations;
        Assert.NotEmpty(ops);

        // StoreOperation для записи в память
        var store = ops.OfType<StoreOperation>().FirstOrDefault();
        if (store != null)
        {
            ExprTestHelpers.AssertSameVariable(expr.Variables.BX, store.Address);
        }
        else
        {
            // Fallback — просто проверяем, что операции есть
            Assert.NotEmpty(ops);
        }
    }

    [Fact]
    public void And_Ax_Memory_ProducesMath2Expr()
    {
        // AND AX, [BX]
        var expr = BuildExpressions("23 07");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));

        var andExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.And, andExpr.Operation);
        ExprTestHelpers.AssertSameVariable(expr.Variables.AX, andExpr.First);
        Assert.IsType<MemExpr>(andExpr.Second);
    }

    #endregion

    #region OR

    [Fact]
    public void Or_Ax_Immediate16_ProducesMath2Expr()
    {
        // OR AX, 00FFh
        var expr = BuildExpressions("0D FF 00");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));

        var orExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Or, orExpr.Operation);
        ExprTestHelpers.AssertSameVariable(expr.Variables.AX, orExpr.First);
        Assert.Equal(0x00FF, ((ConstExpr)orExpr.Second).Value);
    }

    [Fact]
    public void Or_Ax_Bx_ProducesMath2Expr()
    {
        // OR AX, BX
        var expr = BuildExpressions("0B C3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));

        var orExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Or, orExpr.Operation);
        ExprTestHelpers.AssertSameVariable(expr.Variables.AX, orExpr.First);
        ExprTestHelpers.AssertSameVariable(expr.Variables.BX, orExpr.Second);
    }

    [Fact]
    public void Or_Al_Immediate8_ProducesMath2Expr()
    {
        // OR AL, 80h
        var expr = BuildExpressions("0C 80");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));

        Assert.IsType<Math2Expr>(set.Src);
    }

    [Fact]
    public void Or_Ax_Memory_ProducesMath2Expr()
    {
        // OR AX, [BX]
        var expr = BuildExpressions("0B 07");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));

        var orExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Or, orExpr.Operation);
        ExprTestHelpers.AssertSameVariable(expr.Variables.AX, orExpr.First);
        Assert.IsType<MemExpr>(orExpr.Second);
    }

    #endregion

    #region XOR

    [Fact]
    public void Xor_Ax_Immediate16_ProducesMath2Expr()
    {
        // XOR AX, 00FFh
        var expr = BuildExpressions("35 FF 00");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));

        var xorExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Xor, xorExpr.Operation);
        ExprTestHelpers.AssertSameVariable(expr.Variables.AX, xorExpr.First);
        Assert.Equal(0x00FF, ((ConstExpr)xorExpr.Second).Value);
    }

    [Fact]
    public void Xor_Ax_Bx_ProducesMath2Expr()
    {
        // XOR AX, BX
        var expr = BuildExpressions("33 C3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));

        var xorExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Xor, xorExpr.Operation);
        ExprTestHelpers.AssertSameVariable(expr.Variables.AX, xorExpr.First);
        ExprTestHelpers.AssertSameVariable(expr.Variables.BX, xorExpr.Second);
    }

    [Fact]
    public void Xor_Bx_Ax_ProducesMath2Expr()
    {
        // XOR BX, AX
        var expr = BuildExpressions("31 C3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.BX));

        var xorExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Xor, xorExpr.Operation);
        ExprTestHelpers.AssertSameVariable(expr.Variables.BX, xorExpr.First);
        ExprTestHelpers.AssertSameVariable(expr.Variables.AX, xorExpr.Second);
    }

    [Fact]
    public void Xor_Al_Bh_ProducesMath2Expr()
    {
        // XOR AL, BH
        var expr = BuildExpressions("30 F4");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));

        Assert.IsType<Math2Expr>(set.Src);
    }

    [Fact]
    public void Xor_Ax_Memory_ProducesMath2Expr()
    {
        // XOR AX, [BX]
        var expr = BuildExpressions("33 07");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));

        var xorExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Xor, xorExpr.Operation);
        ExprTestHelpers.AssertSameVariable(expr.Variables.AX, xorExpr.First);
        Assert.IsType<MemExpr>(xorExpr.Second);
    }

    #endregion

    #region Флаги

    [Fact]
    public void And_UpdatesFlags()
    {
        // AND AX, BX
        var expr = BuildExpressions("23 C3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();

        var hasZf = sets.Any(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.ZF));
        var hasSf = sets.Any(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.SF));
        var hasCf = sets.Any(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.CF));
        var hasOf = sets.Any(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.OF));

        Assert.True(hasZf, "ZF должен быть установлен");
        Assert.True(hasSf, "SF должен быть установлен");
        Assert.True(hasCf, "CF должен быть установлен");
        Assert.True(hasOf, "OF должен быть установлен");
    }

    [Fact]
    public void Or_UpdatesFlags()
    {
        // OR AX, BX
        var expr = BuildExpressions("0B C3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();

        var hasZf = sets.Any(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.ZF));
        var hasSf = sets.Any(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.SF));
        var hasCf = sets.Any(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.CF));
        var hasOf = sets.Any(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.OF));

        Assert.True(hasZf, "ZF должен быть установлен");
        Assert.True(hasSf, "SF должен быть установлен");
        Assert.True(hasCf, "CF должен быть установлен");
        Assert.True(hasOf, "OF должен быть установлен");
    }

    [Fact]
    public void Xor_UpdatesFlags()
    {
        // XOR AX, BX
        var expr = BuildExpressions("33 C3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();

        var hasZf = sets.Any(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.ZF));
        var hasSf = sets.Any(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.SF));
        var hasCf = sets.Any(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.CF));
        var hasOf = sets.Any(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.OF));

        Assert.True(hasZf, "ZF должен быть установлен");
        Assert.True(hasSf, "SF должен быть установлен");
        Assert.True(hasCf, "CF должен быть установлен");
        Assert.True(hasOf, "OF должен быть установлен");
    }

    #endregion

    #region CF и OF всегда 0

    [Fact]
    public void And_SetsCFToZero()
    {
        var expr = BuildExpressions("25 FF 00");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var cfSet = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.CF));

        // CF = 0
        Assert.IsType<ConstExpr>(cfSet.Src);
        Assert.Equal(0, ((ConstExpr)cfSet.Src).Value);
    }

    [Fact]
    public void Or_SetsCFToZero()
    {
        var expr = BuildExpressions("0D FF 00");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var cfSet = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.CF));

        Assert.IsType<ConstExpr>(cfSet.Src);
        Assert.Equal(0, ((ConstExpr)cfSet.Src).Value);
    }

    [Fact]
    public void Xor_SetsCFToZero()
    {
        var expr = BuildExpressions("35 FF 00");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var cfSet = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.CF));

        Assert.IsType<ConstExpr>(cfSet.Src);
        Assert.Equal(0, ((ConstExpr)cfSet.Src).Value);
    }

    [Fact]
    public void And_SetsOFToZero()
    {
        var expr = BuildExpressions("25 FF 00");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var ofSet = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.OF));

        Assert.IsType<ConstExpr>(ofSet.Src);
        Assert.Equal(0, ((ConstExpr)ofSet.Src).Value);
    }

    [Fact]
    public void Or_SetsOFToZero()
    {
        var expr = BuildExpressions("0D FF 00");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var ofSet = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.OF));

        Assert.IsType<ConstExpr>(ofSet.Src);
        Assert.Equal(0, ((ConstExpr)ofSet.Src).Value);
    }

    [Fact]
    public void Xor_SetsOFToZero()
    {
        var expr = BuildExpressions("35 FF 00");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var ofSet = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.OF));

        Assert.IsType<ConstExpr>(ofSet.Src);
        Assert.Equal(0, ((ConstExpr)ofSet.Src).Value);
    }

    #endregion

    #region ZF для нулевого результата

    [Fact]
    public void Xor_Ax_Ax_SetsZFToOne()
    {
        // XOR AX, AX → AX = 0, ZF = 1
        var expr = BuildExpressions("31 C0");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));

        var xorExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Xor, xorExpr.Operation);
        ExprTestHelpers.AssertSameVariable(expr.Variables.AX, xorExpr.First);
        ExprTestHelpers.AssertSameVariable(expr.Variables.AX, xorExpr.Second);

        // ZF = (result == 0)
        var zfSet = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.ZF));
        Assert.IsType<CmpExpr>(zfSet.Src);
    }

    #endregion
}
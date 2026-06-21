namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты инструкций умножения и деления: MUL, IMUL, DIV, IDIV.
/// Проверяют корректное создание Math2Expr (Mul/Div/Mod) и обновление флагов.
/// </summary>
public class MulDivTests : BaseTests
{
    #region MUL (беззнаковое умножение)

    [Fact]
    public void Mul_Al_Reg8_ProducesMulExpr()
    {
        // MUL BL: AL × BL → AX
        var expr = BuildExpressionsRaw("F6 E3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));

        // AX = AL × BL
        var mulExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Mul, mulExpr.Operation);
    }

    [Fact]
    public void Mul_Al_Immediate8_ProducesMulExpr()
    {
        // MUL byte ptr [0x1234]: AL × mem8 → AX
        var expr = BuildExpressionsRaw("F6 26 34 12");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));

        // AX = AL × mem8
        var mulExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Mul, mulExpr.Operation);
    }

    [Fact]
    public void Mul_Ax_Reg16_ProducesMulExpr()
    {
        // MUL BX: AX × BX → DX:AX
        var expr = BuildExpressionsRaw("F7 E3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var axSet = sets.First(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));

        // AX = AX × BX
        var mulExpr = Assert.IsType<Math2Expr>(axSet.Src);
        Assert.Equal(Math2Operation.Mul, mulExpr.Operation);

        // DX должен быть установлен (старшие 16 бит)
        var dxSet = sets.FirstOrDefault(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.DX));
        Assert.NotNull(dxSet);
    }

    [Fact]
    public void Mul_8Bit_SetsFlags()
    {
        // MUL BL: CF=OF=1, если AH != 0
        var expr = BuildExpressionsRaw("F6 E3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();

        // Должны быть установлены CF и OF
        var hasCf = sets.Any(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.CF));
        var hasOf = sets.Any(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.OF));

        Assert.True(hasCf, "CF должен быть установлен");
        Assert.True(hasOf, "OF должен быть установлен");
    }

    [Fact]
    public void Mul_16Bit_SetsFlags()
    {
        // MUL BX: CF=OF=1, если DX != 0
        var expr = BuildExpressionsRaw("F7 E3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();

        var hasCf = sets.Any(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.CF));
        var hasOf = sets.Any(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.OF));

        Assert.True(hasCf, "CF должен быть установлен");
        Assert.True(hasOf, "OF должен быть установлен");
    }

    #endregion

    #region IMUL (знаковое умножение)

    [Fact]
    public void Imul_Al_Reg8_ProducesMulExpr()
    {
        // IMUL BL: AL × BL → AX (знаковое)
        var expr = BuildExpressionsRaw("F6 EB");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var set = Assert.Single(sets, op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));

        // AX = AL × BL
        var mulExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Mul, mulExpr.Operation);
    }

    [Fact]
    public void Imul_Ax_Reg16_ProducesMulExpr()
    {
        // IMUL BX: AX × BX → DX:AX (знаковое)
        var expr = BuildExpressionsRaw("F7 EB");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var axSet = sets.First(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));

        // AX = AX × BX
        var mulExpr = Assert.IsType<Math2Expr>(axSet.Src);
        Assert.Equal(Math2Operation.Mul, mulExpr.Operation);

        // DX должен быть установлен
        var dxSet = sets.FirstOrDefault(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.DX));
        Assert.NotNull(dxSet);
    }

    [Fact]
    public void Imul_8Bit_SetsFlags()
    {
        // IMUL BL: CF=OF=1, если результат не помещается в 8 бит со знаком
        var expr = BuildExpressionsRaw("F6 EB");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();

        var hasCf = sets.Any(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.CF));
        var hasOf = sets.Any(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.OF));

        Assert.True(hasCf, "CF должен быть установлен");
        Assert.True(hasOf, "OF должен быть установлен");
    }

    [Fact]
    public void Imul_16Bit_SetsFlags()
    {
        // IMUL BX: CF=OF=1, если DX != знаковое расширение AX
        var expr = BuildExpressionsRaw("F7 EB");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();

        var hasCf = sets.Any(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.CF));
        var hasOf = sets.Any(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.OF));

        Assert.True(hasCf, "CF должен быть установлен");
        Assert.True(hasOf, "OF должен быть установлен");
    }

    #endregion

    #region DIV (беззнаковое деление)

    [Fact]
    public void Div_Al_Reg8_ProducesDivAndModExpr()
    {
        // DIV BL: AX / BL → AL, AH = остаток
        var expr = BuildExpressionsRaw("F6 F3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();

        // Проверяем, что есть операции с AX (AL и AH устанавливаются через AX)
        var axSets = sets.Where(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX)).ToList();
        Assert.NotEmpty(axSets);

        // Проверяем, что есть выражения Div и Mod
        var allExprs = axSets.SelectMany(s => GetAllMathOperations(s.Src)).ToList();
        Assert.True(allExprs.Any(e => e.Operation == Math2Operation.Div), "Должна быть операция DIV");
        Assert.True(allExprs.Any(e => e.Operation == Math2Operation.Mod), "Должна быть операция MOD");
    }

    [Fact]
    public void Div_Ax_Reg16_ProducesDivAndModExpr()
    {
        // DIV BX: (DX:AX) / BX → AX, DX = остаток
        var expr = BuildExpressionsRaw("F7 F3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();

        // AX = (DX:AX) / BX
        var axSet = sets.FirstOrDefault(op =>
            AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));
        Assert.NotNull(axSet);

        var axExpr = axSet!.Src as Math2Expr;
        Assert.NotNull(axExpr);
        Assert.Equal(Math2Operation.Div, axExpr.Operation);

        // DX = (DX:AX) % BX
        var dxSet = sets.FirstOrDefault(op =>
            AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.DX));
        Assert.NotNull(dxSet);

        var dxExpr = dxSet!.Src as Math2Expr;
        Assert.NotNull(dxExpr);
        Assert.Equal(Math2Operation.Mod, dxExpr.Operation);
    }

    #endregion

    #region IDIV (знаковое деление)

    [Fact]
    public void Idiv_Al_Reg8_ProducesDivAndModExpr()
    {
        // IDIV BL: AX / BL → AL, AH = остаток (знаковое)
        var expr = BuildExpressionsRaw("F6 FB");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();

        // Проверяем, что есть операции с AX (AL и AH устанавливаются через AX)
        var axSets = sets.Where(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX)).ToList();
        Assert.NotEmpty(axSets);

        // Проверяем, что есть выражения Div и Mod
        var allExprs = axSets.SelectMany(s => GetAllMathOperations(s.Src)).ToList();
        Assert.True(allExprs.Any(e => e.Operation == Math2Operation.Div), "Должна быть операция DIV");
        Assert.True(allExprs.Any(e => e.Operation == Math2Operation.Mod), "Должна быть операция MOD");
    }

    [Fact]
    public void Idiv_Ax_Reg16_ProducesDivAndModExpr()
    {
        // IDIV BX: (DX:AX) / BX → AX, DX = остаток (знаковое)
        var expr = BuildExpressionsRaw("F7 FB");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();

        // AX = (DX:AX) / BX
        var axSet = sets.FirstOrDefault(op =>
            AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));
        Assert.NotNull(axSet);

        var axExpr = axSet!.Src as Math2Expr;
        Assert.NotNull(axExpr);
        Assert.Equal(Math2Operation.Div, axExpr.Operation);

        // DX = (DX:AX) % BX
        var dxSet = sets.FirstOrDefault(op =>
            AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.DX));
        Assert.NotNull(dxSet);

        var dxExpr = dxSet!.Src as Math2Expr;
        Assert.NotNull(dxExpr);
        Assert.Equal(Math2Operation.Mod, dxExpr.Operation);
    }

    #endregion

    #region Helpers

    private static IEnumerable<Math2Expr> GetAllMathOperations(Expr expr)
    {
        if (expr is Math2Expr m2)
        {
            yield return m2;
            foreach (var child in GetAllMathOperations(m2.First))
                yield return child;
            foreach (var child in GetAllMathOperations(m2.Second))
                yield return child;
        }
    }

    #endregion
}

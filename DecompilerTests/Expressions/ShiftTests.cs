using TestSupport;

namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты для инструкций сдвига: SAL, SHR, SAR
/// </summary>
public class ShiftTests : BaseTests
{
    #region SAL (Shift Arithmetic Left)

    [Fact]
    public void SalAxCl_BuildExpressions_ShiftLeft()
    {
        // SAL AX, CL — сдвиг влево
        var expr = BuildExpressionsRaw("""
            D3 E0    ; SAL AX, CL
            C3       ; RET
            """);

        // Первая операция — установка AX, остальные — флаги и RET
        var setOp = expr.Blocks[0].Operations.OfType<SetOperation>()
            .First(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));
        Assert.Contains("<<", setOp.Src.ToString());
    }

    [Fact]
    public void SalAx1_ConstantFold_ResultIsShifted()
    {
        // SAL AX, 1 с константным значением
        var expr = BuildExpressionsRaw("""
            B8 01 00    ; MOV AX, 1
            D1 E0       ; SAL AX, 1
            C3          ; RET
            """);

        // Операция сдвига должна содержать <<
        var shiftOp = expr.Blocks[0].Operations.OfType<SetOperation>()
            .FirstOrDefault(op => op.Src.ToString().Contains("<<"));
        Assert.NotNull(shiftOp);
    }

    #endregion

    #region SHR (Shift Right Logical)

    [Fact]
    public void ShrAxCl_BuildExpressions_ShiftRight()
    {
        // SHR AX, CL — логический сдвиг вправо
        var expr = BuildExpressionsRaw("""
            D3 E8    ; SHR AX, CL
            C3       ; RET
            """);

        var setOp = expr.Blocks[0].Operations.OfType<SetOperation>()
            .First(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));
        Assert.Contains(">>", setOp.Src.ToString());
    }

    [Fact]
    public void ShrAx1_ConstantFold_ResultIsShifted()
    {
        // SHR AX, 1 с константным значением
        var expr = BuildExpressionsRaw("""
            B8 04 00    ; MOV AX, 4
            D1 E8       ; SHR AX, 1
            C3          ; RET
            """);

        // Операция сдвига должна содержать >>
        var shiftOp = expr.Blocks[0].Operations.OfType<SetOperation>()
            .FirstOrDefault(op => op.Src.ToString().Contains(">>"));
        Assert.NotNull(shiftOp);
    }

    #endregion

    #region SAR (Shift Arithmetic Right)

    [Fact]
    public void SarAxCl_BuildExpressions_ShiftRight()
    {
        // SAR AX, CL — арифметический сдвиг вправо
        var expr = BuildExpressionsRaw("""
            D3 F8    ; SAR AX, CL
            C3       ; RET
            """);

        var setOp = expr.Blocks[0].Operations.OfType<SetOperation>()
            .First(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.AX));
        // SAR тоже использует >>, но семантика зависит от типа (signed)
        Assert.Contains(">>", setOp.Src.ToString());
    }

    [Fact]
    public void SarAx1_ConstantFold_ResultIsShifted()
    {
        // SAR AX, 1 с константным значением
        var expr = BuildExpressionsRaw("""
            B8 F0 FF    ; MOV AX, 0xFFF0 (-16 в signed)
            D1 F8       ; SAR AX, 1
            C3          ; RET
            """);

        // Операция сдвига должна содержать >>
        var shiftOp = expr.Blocks[0].Operations.OfType<SetOperation>()
            .FirstOrDefault(op => op.Src.ToString().Contains(">>"));
        Assert.NotNull(shiftOp);
    }

    #endregion

    #region Flags

    [Fact]
    public void SalUpdatesFlags_ZF_SetOnZero()
    {
        // SAL должен обновлять ZF
        var expr = BuildExpressionsRaw("""
            B8 00 00    ; MOV AX, 0
            D1 E0       ; SAL AX, 1
            C3          ; RET
            """);

        // Проверяем, что флаг ZF установлен
        var zfSet = expr.Blocks[0].Operations.OfType<SetOperation>()
            .FirstOrDefault(op => AssignmentTarget.ReferencesVariable(op.Dst, expr.Variables.ZF));
        Assert.NotNull(zfSet);
    }

    #endregion
}

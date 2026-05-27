using UltraDecompiler.Decompilation;

namespace Tests.Expressions;

/// <summary>
/// Тесты ExpressionBuilder для операций сдвига.
/// </summary>
public class ShiftsTests : BaseTests
{
    [Fact]
    public void DecompileSalShiftByOne()
    {
        var expr = BuildExpressions("""
            B8 01 00 ; mov ax, 1
            D1 E0    ; sal ax, 1
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        Assert.Single(block.Operations);

        var e0 = Assert.IsType<SetOperation>(block.Operations[0]);
        var m2 = Assert.IsType<Math2Expr>(e0.Src);
        Assert.Equal(Math2Operation.Shl, m2.Operation);

        var c1 = Assert.IsType<ConstExpr>(m2.Second);
        Assert.Equal(1, c1.Value);

        Assert.Equal(e0.Dst, block.EndRegisters.AX);
    }

    [Fact]
    public void DecompileShrShiftByOne()
    {
        var expr = BuildExpressions("""
            B9 80 00 ; mov cx, 80h
            D1 E9    ; shr cx, 1
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        Assert.Single(block.Operations);

        var e0 = Assert.IsType<SetOperation>(block.Operations[0]);
        var m2 = Assert.IsType<Math2Expr>(e0.Src);
        Assert.Equal(Math2Operation.Shr, m2.Operation);

        Assert.Equal(e0.Dst, block.EndRegisters.CX);
    }

    [Fact]
    public void DecompileSalWithCl()
    {
        // Сдвиг на значение из CL (D3 /4). Сам mov cl не создаёт Operation.
        var expr = BuildExpressions("""
            B8 01 00 ; mov ax, 1
            B1 03    ; mov cl, 3
            D3 E0    ; sal ax, cl
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];

        var e0 = Assert.IsType<SetOperation>(block.Operations[^1]);
        var m2 = Assert.IsType<Math2Expr>(e0.Src);
        Assert.Equal(Math2Operation.Shl, m2.Operation);

        Assert.Equal(e0.Dst, block.EndRegisters.AX);
    }
}

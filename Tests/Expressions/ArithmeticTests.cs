using UltraDecompiler.Decompilation;

namespace Tests.Expressions;

/// <summary>
/// Тесты ExpressionBuilder для арифметических операций.
/// </summary>
public class ArithmeticTests : BaseTests
{
    [Fact]
    public void DecompileSumConstExpression()
    {
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            BB 07 00 ; mov bx, 7
            01 D8    ; add ax, bx
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        // const + const => folded, no SetOperation emitted (same spirit as Calculate)
        Assert.Empty(block.Operations);

        var ax = Assert.IsType<ConstExpr>(block.EndRegisters.AX);
        Assert.Equal(12, ax.Value);
    }

    [Fact]
    public void DecompileSubConstExpression()
    {
        var expr = BuildExpressions("""
            B8 0A 00 ; mov ax, 10
            BB 03 00 ; mov bx, 3
            29 D8    ; sub ax, bx
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        // const - const => folded, no SetOperation
        Assert.Empty(block.Operations);

        var ax = Assert.IsType<ConstExpr>(block.EndRegisters.AX);
        Assert.Equal(7, ax.Value);
    }

    [Fact]
    public void DecompileIncAx()
    {
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            40       ; inc ax
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        // inc on const => folded to 6, no SetOperation
        Assert.Empty(block.Operations);

        var ax = Assert.IsType<ConstExpr>(block.EndRegisters.AX);
        Assert.Equal(6, ax.Value);
    }

    [Fact]
    public void DecompileDecBx()
    {
        var expr = BuildExpressions("""
            BB 0A 00 ; mov bx, 10
            4B       ; dec bx
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        // dec on const => folded, no SetOperation
        Assert.Empty(block.Operations);

        var bx = Assert.IsType<ConstExpr>(block.EndRegisters.BX);
        Assert.Equal(9, bx.Value);
    }

    [Fact]
    public void DecompileAddToCx()
    {
        var expr = BuildExpressions("""
            B9 0A 00 ; mov cx, 10
            BA 14 00 ; mov dx, 20
            01 D1    ; add cx, dx
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        // const + const folded, no operation
        Assert.Empty(block.Operations);

        var cx = Assert.IsType<ConstExpr>(block.EndRegisters.CX);
        Assert.Equal(30, cx.Value);
    }
}

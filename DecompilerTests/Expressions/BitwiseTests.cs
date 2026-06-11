using UltraDecompiler.Decompilation;

namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты ExpressionBuilder для побитовых и унарных операций.
/// </summary>
public class BitwiseTests : BaseTests
{
    // and ax, 0Fh: 0xFF & 0x0F → AX=0x0F, свёртка
    [Fact]
    public void DecompileAndExpression()
    {
        var expr = BuildExpressions("""
            B8 FF 00 ; mov ax, 0FFh
            25 0F 00 ; and ax, 0Fh
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        // 0xFF & 0x0F => 0x0F const, folded, no Set
        Assert.Empty(block.Operations);

        var ax = Assert.IsType<ConstExpr>(block.EndRegisters.AX);
        Assert.Equal(0x0F, ax.Value);
    }

    // or ax, 1: 0x10 | 1 → 0x11
    [Fact]
    public void DecompileOrExpression()
    {
        var expr = BuildExpressions("""
            B8 10 00 ; mov ax, 10h
            0D 01 00 ; or ax, 1
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        // 0x10 | 1 => 0x11 const, no Set
        Assert.Empty(block.Operations);

        var ax = Assert.IsType<ConstExpr>(block.EndRegisters.AX);
        Assert.Equal(0x11, ax.Value);
    }

    // xor ax, 0Fh: 0xFF ^ 0x0F → 0xF0
    [Fact]
    public void DecompileXorExpression()
    {
        var expr = BuildExpressions("""
            B8 FF 00 ; mov ax, 0FFh
            35 0F 00 ; xor ax, 0Fh
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        // 0xFF ^ 0x0F => 0xF0 const, no Set
        Assert.Empty(block.Operations);

        var ax = Assert.IsType<ConstExpr>(block.EndRegisters.AX);
        Assert.Equal(0xF0, ax.Value);
    }

    // not al: ~5 в AL
    [Fact]
    public void DecompileNotExpression()
    {
        var expr = BuildExpressions("""
            B0 05    ; mov al, 5
            F6 D0    ; not al
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        // not 5 (const) => ~5 folded, no SetOperation
        Assert.Empty(block.Operations);

        // AL holds the folded const (~5)
        var al = Assert.IsType<ConstExpr>(block.EndRegisters.AL);
        Assert.Equal(~5, al.Value);
    }

    // neg bx: -5 в BX
    [Fact]
    public void DecompileNegExpression()
    {
        var expr = BuildExpressions("""
            BB 05 00 ; mov bx, 5
            F7 DB    ; neg bx
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        // neg 5 => -5 const folded, no Set
        Assert.Empty(block.Operations);

        var bx = Assert.IsType<ConstExpr>(block.EndRegisters.BX);
        Assert.Equal(-5, bx.Value);
    }
}

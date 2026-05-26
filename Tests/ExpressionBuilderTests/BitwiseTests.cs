using UltraDecompiler.Decompilation;

namespace Tests.ExpressionBuilderTests;

/// <summary>
/// Тесты ExpressionBuilder для побитовых и унарных операций.
/// </summary>
public class BitwiseTests : BaseTests
{
    [Fact]
    public void DecompileAndExpression()
    {
        var expr = BuildExpressions("""
            B8 FF 00 ; mov ax, 0FFh
            25 0F 00 ; and ax, 0Fh
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        Assert.Single(block.Operations);

        var e0 = Assert.IsType<SetOperation>(block.Operations[0]);
        var m2 = Assert.IsType<Math2Expr>(e0.Src);
        Assert.Equal(Math2Operation.And, m2.Operation);

        var c1 = Assert.IsType<ConstExpr>(m2.Second);
        Assert.Equal(0x0F, c1.Value);

        Assert.Equal(e0.Dst, block.EndRegisters.AX);
    }

    [Fact]
    public void DecompileOrExpression()
    {
        var expr = BuildExpressions("""
            B8 10 00 ; mov ax, 10h
            0D 01 00 ; or ax, 1
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        Assert.Single(block.Operations);

        var e0 = Assert.IsType<SetOperation>(block.Operations[0]);
        var m2 = Assert.IsType<Math2Expr>(e0.Src);
        Assert.Equal(Math2Operation.Or, m2.Operation);

        Assert.Equal(e0.Dst, block.EndRegisters.AX);
    }

    [Fact]
    public void DecompileXorExpression()
    {
        var expr = BuildExpressions("""
            B8 FF 00 ; mov ax, 0FFh
            35 0F 00 ; xor ax, 0Fh
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        Assert.Single(block.Operations);

        var e0 = Assert.IsType<SetOperation>(block.Operations[0]);
        var m2 = Assert.IsType<Math2Expr>(e0.Src);
        Assert.Equal(Math2Operation.Xor, m2.Operation);

        var c1 = Assert.IsType<ConstExpr>(m2.Second);
        Assert.Equal(0x0F, c1.Value);

        Assert.Equal(e0.Dst, block.EndRegisters.AX);
    }

    [Fact]
    public void DecompileNotExpression()
    {
        var expr = BuildExpressions("""
            B0 05    ; mov al, 5
            F6 D0    ; not al
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        Assert.Single(block.Operations);

        var e0 = Assert.IsType<SetOperation>(block.Operations[0]);
        var m1 = Assert.IsType<Math1Expr>(e0.Src);
        Assert.Equal(Math1Operation.Not, m1.Operation);
    }

    [Fact]
    public void DecompileNegExpression()
    {
        var expr = BuildExpressions("""
            BB 05 00 ; mov bx, 5
            F7 DB    ; neg bx
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        Assert.Single(block.Operations);

        var e0 = Assert.IsType<SetOperation>(block.Operations[0]);
        var m1 = Assert.IsType<Math1Expr>(e0.Src);
        Assert.Equal(Math1Operation.Neg, m1.Operation);

        Assert.Equal(e0.Dst, block.EndRegisters.BX);
    }
}

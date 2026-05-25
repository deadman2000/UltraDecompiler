using UltraDecompiler.Decompilation;

namespace Tests;

public class DecompilerTests : BaseTests
{
    [Fact]
    public void DecompileSumConstExpression()
    {
        var decomp = Decompile("""
            B8 05 00 ; mov ax, 5
            BB 07 00 ; mov bx, 7
            01 D8    ; add ax, bx
            """);

        Assert.Single(decomp.Blocks);
        var block = decomp.Blocks[0];
        Assert.Single(block.Operations);

        var e0 = Assert.IsType<SetOperation>(block.Operations[0]);
        var m2 = Assert.IsType<Math2Expr>(e0.Src);
        Assert.Equal(Math2Operation.Add, m2.Operation);

        var c0 = Assert.IsType<ConstExpr>(m2.First);
        var c1 = Assert.IsType<ConstExpr>(m2.Second);

        Assert.Equal(5, c0.Value);
        Assert.Equal(7, c1.Value);
        
        Assert.Equal(e0.Dst, block.EndRegisters.AX);
    }

    [Fact]
    public void DecompileSubConstExpression()
    {
        var decomp = Decompile("""
            B8 0A 00 ; mov ax, 10
            BB 03 00 ; mov bx, 3
            29 D8    ; sub ax, bx
            """);

        Assert.Single(decomp.Blocks);
        var block = decomp.Blocks[0];
        Assert.Single(block.Operations);

        var e0 = Assert.IsType<SetOperation>(block.Operations[0]);
        var m2 = Assert.IsType<Math2Expr>(e0.Src);
        Assert.Equal(Math2Operation.Sub, m2.Operation);

        var c0 = Assert.IsType<ConstExpr>(m2.First);
        var c1 = Assert.IsType<ConstExpr>(m2.Second);

        Assert.Equal(10, c0.Value);
        Assert.Equal(3, c1.Value);
        
        Assert.Equal(e0.Dst, block.EndRegisters.AX);
    }

    [Fact]
    public void DecompileIncAx()
    {
        var decomp = Decompile("""
            B8 05 00 ; mov ax, 5
            40       ; inc ax
            """);

        Assert.Single(decomp.Blocks);
        var block = decomp.Blocks[0];
        Assert.Single(block.Operations);

        var e0 = Assert.IsType<SetOperation>(block.Operations[0]);
        var m2 = Assert.IsType<Math2Expr>(e0.Src);
        Assert.Equal(Math2Operation.Add, m2.Operation);

        var c1 = Assert.IsType<ConstExpr>(m2.Second);
        Assert.Equal(1, c1.Value);
        
        Assert.Equal(e0.Dst, block.EndRegisters.AX);
    }

    [Fact]
    public void DecompileDecBx()
    {
        var decomp = Decompile("""
            BB 0A 00 ; mov bx, 10
            4B       ; dec bx
            """);

        Assert.Single(decomp.Blocks);
        var block = decomp.Blocks[0];
        Assert.Single(block.Operations);

        var e0 = Assert.IsType<SetOperation>(block.Operations[0]);
        var m2 = Assert.IsType<Math2Expr>(e0.Src);
        Assert.Equal(Math2Operation.Sub, m2.Operation);

        var c1 = Assert.IsType<ConstExpr>(m2.Second);
        Assert.Equal(1, c1.Value);
        
        Assert.Equal(e0.Dst, block.EndRegisters.BX);
    }

    [Fact]
    public void DecompileAddToCx()
    {
        var decomp = Decompile("""
            B9 0A 00 ; mov cx, 10
            BA 14 00 ; mov dx, 20
            01 D1    ; add cx, dx
            """);

        Assert.Single(decomp.Blocks);
        var block = decomp.Blocks[0];
        Assert.Single(block.Operations);

        var e0 = Assert.IsType<SetOperation>(block.Operations[0]);
        var m2 = Assert.IsType<Math2Expr>(e0.Src);
        Assert.Equal(Math2Operation.Add, m2.Operation);

        Assert.Equal(e0.Dst, block.EndRegisters.CX);
    }

    [Fact]
    public void DecompileMultipleBlocksWithJmp()
    {
        var decomp = Decompile("""
            B8 01 00 ; mov ax, 1
            EB 00    ; jmp short +0
            90       ; nop (в следующем блоке)
            """);

        Assert.Equal(2, decomp.Blocks.Count);
        var block0 = decomp.Blocks[0];
        var block1 = decomp.Blocks[1];

        Assert.NotNull(block0.Next);
        Assert.Equal(block1, block0.Next);
        Assert.Null(block0.ConditionalBlock);
    }
}
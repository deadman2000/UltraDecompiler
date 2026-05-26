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

    [Fact]
    public void DecompileAndExpression()
    {
        var decomp = Decompile("""
            B8 FF 00 ; mov ax, 0FFh
            25 0F 00 ; and ax, 0Fh
            """);

        Assert.Single(decomp.Blocks);
        var block = decomp.Blocks[0];
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
        var decomp = Decompile("""
            B8 10 00 ; mov ax, 10h
            0D 01 00 ; or ax, 1
            """);

        Assert.Single(decomp.Blocks);
        var block = decomp.Blocks[0];
        Assert.Single(block.Operations);

        var e0 = Assert.IsType<SetOperation>(block.Operations[0]);
        var m2 = Assert.IsType<Math2Expr>(e0.Src);
        Assert.Equal(Math2Operation.Or, m2.Operation);

        Assert.Equal(e0.Dst, block.EndRegisters.AX);
    }

    [Fact]
    public void DecompileXorExpression()
    {
        var decomp = Decompile("""
            B8 FF 00 ; mov ax, 0FFh
            35 0F 00 ; xor ax, 0Fh
            """);

        Assert.Single(decomp.Blocks);
        var block = decomp.Blocks[0];
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
        var decomp = Decompile("""
            B0 05    ; mov al, 5
            F6 D0    ; not al
            """);

        Assert.Single(decomp.Blocks);
        var block = decomp.Blocks[0];
        Assert.Single(block.Operations);

        var e0 = Assert.IsType<SetOperation>(block.Operations[0]);
        var m1 = Assert.IsType<Math1Expr>(e0.Src);
        Assert.Equal(Math1Operation.Not, m1.Operation);
    }

    [Fact]
    public void DecompileNegExpression()
    {
        var decomp = Decompile("""
            BB 05 00 ; mov bx, 5
            F7 DB    ; neg bx
            """);

        Assert.Single(decomp.Blocks);
        var block = decomp.Blocks[0];
        Assert.Single(block.Operations);

        var e0 = Assert.IsType<SetOperation>(block.Operations[0]);
        var m1 = Assert.IsType<Math1Expr>(e0.Src);
        Assert.Equal(Math1Operation.Neg, m1.Operation);

        Assert.Equal(e0.Dst, block.EndRegisters.BX);
    }

    [Fact]
    public void DecompileSalShiftByOne()
    {
        var decomp = Decompile("""
            B8 01 00 ; mov ax, 1
            D1 E0    ; sal ax, 1
            """);

        Assert.Single(decomp.Blocks);
        var block = decomp.Blocks[0];
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
        var decomp = Decompile("""
            B9 80 00 ; mov cx, 80h
            D1 E9    ; shr cx, 1
            """);

        Assert.Single(decomp.Blocks);
        var block = decomp.Blocks[0];
        Assert.Single(block.Operations);

        var e0 = Assert.IsType<SetOperation>(block.Operations[0]);
        var m2 = Assert.IsType<Math2Expr>(e0.Src);
        Assert.Equal(Math2Operation.Shr, m2.Operation);

        Assert.Equal(e0.Dst, block.EndRegisters.CX);
    }

    [Fact]
    public void DecompileSalWithCl()
    {
        // Сдвиг на значение из CL (D3 /4). Сам mov cl не создаёт Operation (текущее поведение).
        var decomp = Decompile("""
            B8 01 00 ; mov ax, 1
            B1 03    ; mov cl, 3
            D3 E0    ; sal ax, cl
            """);

        Assert.Single(decomp.Blocks);
        var block = decomp.Blocks[0];

        // Последняя операция должна быть сдвигом
        var e0 = Assert.IsType<SetOperation>(block.Operations[^1]);
        var m2 = Assert.IsType<Math2Expr>(e0.Src);
        Assert.Equal(Math2Operation.Shl, m2.Operation);

        Assert.Equal(e0.Dst, block.EndRegisters.AX);
    }
}
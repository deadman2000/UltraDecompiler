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
        Assert.Equal(3, block.Operations.Count);

        var e0 = Assert.IsType<SetOperation>(block.Operations[0]);
        var c0 = Assert.IsType<ConstExpr>(e0.Src);
        Assert.Equal(5, c0.Value);

        var e1 = Assert.IsType<SetOperation>(block.Operations[1]);
        var c1 = Assert.IsType<ConstExpr>(e1.Src);
        Assert.Equal(7, c1.Value);

        var e2 = Assert.IsType<SetOperation>(block.Operations[2]);
        var m2 = Assert.IsType<Math2Expr>(e2.Src);
        Assert.Equal(Math2Operation.Add, m2.Operation);
        Assert.Equal(c0, m2.First);
        Assert.Equal(c1, m2.Second);

        Assert.Equal(e2.Dst, block.EndRegisters.AX);
    }
}

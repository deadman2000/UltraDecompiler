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
}

using UltraDecompiler.Decompilation;

namespace Tests.Expressions;

/// <summary>
/// Тесты ExpressionBuilder для сравнений (CMP/TEST) и работы с флагами.
/// </summary>
public class ComparisonTests : BaseTests
{
    [Fact]
    public void DecompileCmpSetsZfAsEquality()
    {
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            3D 05 00 ; cmp ax, 5
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        Assert.Empty(block.Operations);

        var zf = block.EndRegisters.ZF;
        var cmp = Assert.IsType<CmpExpr>(zf);
        Assert.Equal(CmpOperation.Eq, cmp.Operation);

        var left = Assert.IsType<ConstExpr>(cmp.Left);
        var right = Assert.IsType<ConstExpr>(cmp.Right);
        Assert.Equal(5, left.Value);
        Assert.Equal(5, right.Value);
    }

    [Fact]
    public void DecompileCmpBetweenRegisters()
    {
        var expr = BuildExpressions("""
            B8 10 00 ; mov ax, 10h
            BB 20 00 ; mov bx, 20h
            39 D8    ; cmp ax, bx
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        Assert.Empty(block.Operations);

        var zf = Assert.IsType<CmpExpr>(block.EndRegisters.ZF);
        Assert.Equal(CmpOperation.Eq, zf.Operation);

        Assert.Equal(block.EndRegisters.AX, zf.Left);
        Assert.Equal(block.EndRegisters.BX, zf.Right);
    }

    [Fact]
    public void DecompileTestSetsZfAndClearsCarryOverflow()
    {
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            A9 01 00 ; test ax, 1
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        Assert.Empty(block.Operations);

        var zf = Assert.IsType<CmpExpr>(block.EndRegisters.ZF);
        Assert.Equal(CmpOperation.Eq, zf.Operation);

        // ax=5 & 1 => const 1 (folded by Calculate in HandleTest)
        var left = Assert.IsType<ConstExpr>(zf.Left);
        Assert.Equal(1, left.Value);
        var zero = Assert.IsType<ConstExpr>(zf.Right);
        Assert.Equal(0, zero.Value);

        var cf = Assert.IsType<ConstExpr>(block.EndRegisters.CF);
        var of = Assert.IsType<ConstExpr>(block.EndRegisters.OF);
        Assert.Equal(0, cf.Value);
        Assert.Equal(0, of.Value);
    }

    [Fact]
    public void DecompileTestAxAxCommonIdiom()
    {
        var expr = BuildExpressions("""
            B8 00 00 ; mov ax, 0
            85 C0    ; test ax, ax
            """);

        var zf = Assert.IsType<CmpExpr>(expr.Blocks[0].EndRegisters.ZF);
        // test 0,0 => and folded to const 0
        var left = Assert.IsType<ConstExpr>(zf.Left);
        Assert.Equal(0, left.Value);
    }

    [Fact]
    public void DecompileCmpJaeFlagVariable()
    {
        // ожидается проверка нижнего байта результата dos_get_dos_version()
        var expr = BuildExpressions("""
            B4 30 ; mov AH, 30h
            CD 21 ; int   21h
            3C 02 ; cmp AL, 2
            73 01 ; jae   3
            90    ; nop
            90    ; nop
            """);

        Assert.NotNull(expr.Blocks[0].Condition);
        var cond = Assert.IsType<CmpExpr>(expr.Blocks[0].Condition);
        Assert.Equal("var4 & 255 == 2", cond.ToString());
    }
}

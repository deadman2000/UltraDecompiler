using UltraDecompiler.Decompilation;

namespace DecompilerTests.Expressions;

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
        // 1 << 1 => 2 const, folded, no Set
        Assert.Empty(block.Operations);

        var ax = Assert.IsType<ConstExpr>(block.EndRegisters.AX);
        Assert.Equal(2, ax.Value);
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
        // 0x80 >> 1 => 0x40 const, no Set
        Assert.Empty(block.Operations);

        var cx = Assert.IsType<ConstExpr>(block.EndRegisters.CX);
        Assert.Equal(0x40, cx.Value);
    }

    [Fact]
    public void DecompileSalWithCl()
    {
        // Сдвиг на значение из CL (D3 /4). mov'ы const => sal 1<<3 тоже const, всё folded.
        var expr = BuildExpressions("""
            B8 01 00 ; mov ax, 1
            B1 03    ; mov cl, 3
            D3 E0    ; sal ax, cl
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];

        // Полностью статическое: 1 << 3 = 8, без SetOperation
        Assert.Empty(block.Operations);

        var ax = Assert.IsType<ConstExpr>(block.EndRegisters.AX);
        Assert.Equal(8, ax.Value);
    }
}

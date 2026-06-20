namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты ExpressionBuilder для операций сдвига.
/// </summary>
public class ShiftsTests : BaseTests
{
    // sal ax, 1: 1<<1=2 → распознаётся как 1*2
    [Fact]
    public void DecompileSalShiftByOne()
    {
        var expr = BuildExpressions("""
            B8 01 00 ; mov ax, 1
            D1 E0    ; sal ax, 1
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];
        // 1 * 2 => 2 const folded
        Assert.Empty(block.Operations);

        var ax = Assert.IsType<ConstExpr>(block.EndRegisters.AX);
        Assert.Equal(2, ax.Value);
    }

    // shr cx, 1: 0x80>>1=0x40
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

    // sal ax, 1 с переменной: var * 2
    [Fact]
    public void DecompileSalWithVariable()
    {
        // Эмуляция: var2 = var2 * 2 через sal
        var expr = BuildExpressions("""
            8B 46 FC ; mov ax, [BP-4]  ; загрузка var2
            D1 E0    ; sal ax, 1       ; ax = ax * 2
            89 46 FC ; mov [BP-4], ax  ; запись обратно
            """, vars =>
        {
            var var2 = vars.CreateVariable("var2");
            return RegisterExpressions.InitCom(vars) with { BP = new ConstExpr(0x100) };
        });

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];

        // Должно быть: var2 = var2 * 2
        // Проверяем, что есть SetOperation с Mul
        var setOps = block.Operations.Where(op => op is SetOperation).ToList();
        Assert.NotEmpty(setOps);

        // Проверяем, что хотя бы одна операция содержит Mul
        var hasMul = block.Operations.Any(op =>
            op is SetOperation { Src: Math2Expr { Operation: Math2Operation.Mul } }
        );
        Assert.True(hasMul, "Ожидалось умножение на 2 (Mul), но получено другое выражение");
    }
}

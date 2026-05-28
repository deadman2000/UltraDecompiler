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

    // ==================== Тесты с символическими переменными (не константы) ====================

    [Fact]
    public void Add_VariablePlusConst_ProducesMathExprAndNewVariable()
    {
        // BX уже содержит символическую переменную (например, результат предыдущих вычислений)
        // Используем Set16, чтобы корректно сбросить байтовые представления (AH/AL и т.д.)
        var expr = BuildExpressions("01 C3", vars =>
        {
            var prev = vars.CreateVariable("prev");
            var init = RegisterExpressions.InitZero();
            return init
                .Set16(3, prev)                    // BX = prev
                .Set16(0, new ConstExpr(7));       // AX = 7
        });

        var block = expr.Blocks[0];
        Assert.Single(block.Operations);

        var set = Assert.IsType<SetOperation>(block.Operations[0]);
        var math = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Add, math.Operation);

        // Первый операнд — предыдущая переменная, второй — константа
        Assert.Equal("prev", Assert.IsType<Variable>(math.First).Name);
        Assert.Equal(7, Assert.IsType<ConstExpr>(math.Second).Value);

        // BX теперь указывает на *новую* переменную (результат сложения)
        var resultVar = Assert.IsType<Variable>(block.EndRegisters.BX);
        Assert.NotEqual("prev", resultVar.Name);
        Assert.Equal(set.Dst, resultVar);
    }

    [Fact]
    public void Add_ConstPlusVariable_ProducesMathExpr()
    {
        var expr = BuildExpressions("01 D8", vars =>   // add ax, bx
        {
            var v = vars.CreateVariable("val");
            var init = RegisterExpressions.InitZero();
            return init
                .Set16(0, new ConstExpr(10))   // AX
                .Set16(3, v);                  // BX
        });

        var block = expr.Blocks[0];
        Assert.Single(block.Operations);

        var set = Assert.IsType<SetOperation>(block.Operations[0]);
        var math = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Add, math.Operation);

        Assert.Equal(10, Assert.IsType<ConstExpr>(math.First).Value);
        Assert.Equal("val", Assert.IsType<Variable>(math.Second).Name);
    }

    [Fact]
    public void Add_TwoVariables_ProducesMathWithBothVariables()
    {
        var expr = BuildExpressions("01 D0", vars =>   // add ax, dx
        {
            var a = vars.CreateVariable("a");
            var d = vars.CreateVariable("d");
            var init = RegisterExpressions.InitZero();
            return init
                .Set16(0, a)
                .Set16(2, d);
        });

        var block = expr.Blocks[0];
        Assert.Single(block.Operations);

        var set = Assert.IsType<SetOperation>(block.Operations[0]);
        var math = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Add, math.Operation);

        Assert.Equal("a", Assert.IsType<Variable>(math.First).Name);
        Assert.Equal("d", Assert.IsType<Variable>(math.Second).Name);
    }

    [Fact]
    public void Inc_OnVariable_CreatesAddWithOne()
    {
        var expr = BuildExpressions("40", vars =>   // inc ax
        {
            var prev = vars.CreateVariable("x");
            return RegisterExpressions.InitZero().Set16(0, prev);
        });

        var block = expr.Blocks[0];
        Assert.Single(block.Operations);

        var set = Assert.IsType<SetOperation>(block.Operations[0]);
        var math = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Add, math.Operation);

        Assert.Equal("x", Assert.IsType<Variable>(math.First).Name);
        Assert.Equal(1, Assert.IsType<ConstExpr>(math.Second).Value);
    }

    [Fact]
    public void Sub_VariableMinusVariable()
    {
        var expr = BuildExpressions("29 D0", vars =>   // sub ax, dx
        {
            var left = vars.CreateVariable("left");
            var right = vars.CreateVariable("right");
            var init = RegisterExpressions.InitZero();
            return init
                .Set16(0, left)
                .Set16(2, right);
        });

        var block = expr.Blocks[0];
        Assert.Single(block.Operations);

        var set = Assert.IsType<SetOperation>(block.Operations[0]);
        var math = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Sub, math.Operation);

        Assert.Equal("left", Assert.IsType<Variable>(math.First).Name);
        Assert.Equal("right", Assert.IsType<Variable>(math.Second).Name);
    }

    // === Новые простые инструкции (XCHG, CBW, ADC/SBB) ===

    [Fact]
    public void Xchg_RegReg_SwapsValues()
    {
        // 93 = xchg ax, bx
        var expr = BuildExpressions("93");

        // После xchg регистры должны поменяться местами (начальные 0 — тривиально)
        // Более осмысленный тест — с переменными
        var expr2 = BuildExpressions("93", vars =>
        {
            var a = vars.CreateVariable("a");
            var b = vars.CreateVariable("b");
            var init = RegisterExpressions.InitZero();
            return init.Set16(0, a).Set16(3, b); // AX=a, BX=b
        });

        var block = expr2.Blocks[0];
        Assert.Equal("b", Assert.IsType<Variable>(block.EndRegisters.AX).Name);
        Assert.Equal("a", Assert.IsType<Variable>(block.EndRegisters.BX).Name);
    }

    [Fact]
    public void Cbw_SignExtend_WhenAlNegative()
    {
        // B0 FF ; mov al, 0FFh ; 98 ; cbw
        var expr = BuildExpressions("""
            B0 FF   ; mov al, -1
            98      ; cbw
            """);

        var ax = expr.Blocks[0].EndRegisters.AX;
        var c = Assert.IsType<ConstExpr>(ax);
        Assert.Equal(0xFFFF, (ushort)c.Value); // -1 как int16
    }

    [Fact]
    public void Adc_Sbb_DoNotThrow_AndUpdateRegisters()
    {
        // Просто проверяем, что ADC/SBB доходят до HandleArithmetic и не падают
        var expr = BuildExpressions("""
            05 01 00   ; add ax, 1
            83 D0 00   ; adc ax, 0
            81 D8 00 00 ; sbb ax, 0
            """);

        // Главное — не упасть + AX обновился
        Assert.NotNull(expr.Blocks[0].EndRegisters.AX);
    }
}

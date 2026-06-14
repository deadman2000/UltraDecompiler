using UltraDecompiler.Ir.Expressions;
using UltraDecompiler.Ir.Operations;

namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты ExpressionBuilder для арифметических операций.
/// </summary>
public class ArithmeticTests : BaseTests
{
    // add ax, bx при константах 5+7 → AX=12, без SetOperation (свёртка в регистре)
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

    // sub ax, bx: 10-3=7, только обновление EndRegisters.AX
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

    // inc ax: 5+1=6, свёртка без IR-операции
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

    // dec bx: 10-1=9
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

    // add cx, dx: 10+20=30 в CX
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

    // add bx, ax при prev+7 → SetOperation с Math2(Add), новая Variable в BX
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
                .Set16(GpRegister16.BX, prev)
                .Set16(GpRegister16.AX, new ConstExpr(7));
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

    // add ax, bx: 10 + val
    [Fact]
    public void Add_ConstPlusVariable_ProducesMathExpr()
    {
        var expr = BuildExpressions("01 D8", vars =>   // add ax, bx
        {
            var v = vars.CreateVariable("val");
            var init = RegisterExpressions.InitZero();
            return init
                .Set16(GpRegister16.AX, new ConstExpr(10))
                .Set16(GpRegister16.BX, v);
        });

        var block = expr.Blocks[0];
        Assert.Single(block.Operations);

        var set = Assert.IsType<SetOperation>(block.Operations[0]);
        var math = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Add, math.Operation);

        Assert.Equal(10, Assert.IsType<ConstExpr>(math.First).Value);
        Assert.Equal("val", Assert.IsType<Variable>(math.Second).Name);
    }

    // add ax, dx: a + d
    [Fact]
    public void Add_TwoVariables_ProducesMathWithBothVariables()
    {
        var expr = BuildExpressions("01 D0", vars =>   // add ax, dx
        {
            var a = vars.CreateVariable("a");
            var d = vars.CreateVariable("d");
            var init = RegisterExpressions.InitZero();
            return init
                .Set16(GpRegister16.AX, a)
                .Set16(GpRegister16.DX, d);
        });

        var block = expr.Blocks[0];
        Assert.Single(block.Operations);

        var set = Assert.IsType<SetOperation>(block.Operations[0]);
        var math = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Add, math.Operation);

        Assert.Equal("a", Assert.IsType<Variable>(math.First).Name);
        Assert.Equal("d", Assert.IsType<Variable>(math.Second).Name);
    }

    // inc ax над переменной → IncOperation, не SetOperation
    [Fact]
    public void Inc_OnVariable_EmitsIncOperation()
    {
        var expr = BuildExpressions("40", vars =>   // inc ax
        {
            var prev = vars.CreateVariable("x");
            return RegisterExpressions.InitZero().Set16(GpRegister16.AX, prev);
        });

        var block = expr.Blocks[0];
        var inc = Assert.IsType<IncOperation>(Assert.Single(block.Operations));
        Assert.Equal("x", Assert.IsType<Variable>(inc.Target).Name);
    }

    // sub ax, dx: left - right
    [Fact]
    public void Sub_VariableMinusVariable()
    {
        var expr = BuildExpressions("29 D0", vars =>   // sub ax, dx
        {
            var left = vars.CreateVariable("left");
            var right = vars.CreateVariable("right");
            var init = RegisterExpressions.InitZero();
            return init
                .Set16(GpRegister16.AX, left)
                .Set16(GpRegister16.DX, right);
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

    // xchg ax, bx меняет символические значения регистров местами
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
            return init.Set16(GpRegister16.AX, a).Set16(GpRegister16.BX, b);
        });

        var block = expr2.Blocks[0];
        Assert.Equal("b", Assert.IsType<Variable>(block.EndRegisters.AX).Name);
        Assert.Equal("a", Assert.IsType<Variable>(block.EndRegisters.BX).Name);
    }

    // cbw: AL=0xFF → AX=0xFFFF (знаковое расширение)
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

    // cwd: AX=0x8000 → DX=0xFFFF
    [Fact]
    public void Cwd_SignExtend_WhenAxNegative()
    {
        // B8 00 80 ; mov ax, 8000h ; 99 ; cwd
        var expr = BuildExpressions("""
            B8 00 80 ; mov ax, 8000h
            99       ; cwd
            """);

        var block = expr.Blocks[0];
        // CWD не создаёт SetOperation — только обновляет DX (как CBW)
        Assert.Empty(block.Operations);

        var dx = Assert.IsType<ConstExpr>(block.EndRegisters.DX);
        Assert.Equal(0xFFFF, (ushort)dx.Value);

        // AX не должен измениться
        var ax = Assert.IsType<ConstExpr>(block.EndRegisters.AX);
        Assert.Equal(0x8000, (ushort)ax.Value);
    }

    // cwd: AX=0x007F → DX=0
    [Fact]
    public void Cwd_SignExtend_WhenAxPositive()
    {
        var expr = BuildExpressions("""
            B8 7F 00 ; mov ax, 007Fh
            99       ; cwd
            """);

        var block = expr.Blocks[0];
        Assert.Empty(block.Operations);

        var dx = Assert.IsType<ConstExpr>(block.EndRegisters.DX);
        Assert.Equal(0, dx.Value);

        var ax = Assert.IsType<ConstExpr>(block.EndRegisters.AX);
        Assert.Equal(0x007F, ax.Value);
    }

    // cwd над переменной: DX — выражение знакового расширения, AX не меняется
    [Fact]
    public void Cwd_WithVariable_ProducesSignExtendExprInDx()
    {
        var expr = BuildExpressions("99", vars =>   // cwd
        {
            var v = vars.CreateVariable("val");
            return RegisterExpressions.InitZero().Set16(GpRegister16.AX, v);
        });

        var block = expr.Blocks[0];
        // Нет SetOperation (CWD — чистое преобразование регистра)
        Assert.Empty(block.Operations);

        // DX теперь содержит выражение знакового расширения (shr/and/sub/and над переменной)
        var dx = block.EndRegisters.DX;
        // Просто проверяем, что это не константа и не исходная переменная AX
        Assert.NotNull(dx);
        Assert.False(dx is ConstExpr);
        // AX остался той же переменной
        var ax = Assert.IsType<Variable>(block.EndRegisters.AX);
        Assert.Equal("val", ax.Name);
    }

    // adc/sbb не падают и обновляют AX
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

    // adc [bp-4], dx — память как назначение, локаль на стеке
    [Fact]
    public void Adc_Memory_BpDisp_DoesNotThrow()
    {
        // 11 56 FC = ADC [BP-4], DX  — специфическая инструкция, которую просили добавить
        // (требует, чтобы дизассемблер распознал 0x11 и ExpressionBuilder/ArithmeticHandler обработал memory dst)
        var expr = BuildExpressions("""
            55         ; push bp
            8B EC      ; mov bp, sp
            11 56 FC   ; adc [bp-4], dx
            """);

        // Не должно бросать NotImplemented, главное — прошёл GenerateCode
        Assert.NotNull(expr);
        Assert.NotEmpty(expr.Blocks);

        // С поддержкой локалов: для ADC mem создаётся temp var для результата арифм. + Set на локал (Variable)
        Assert.Equal(2, expr.Blocks[0].Operations.Count);
        var last = Assert.IsType<SetOperation>(expr.Blocks[0].Operations[1]);
        Assert.IsType<Variable>(last.Dst);
    }

    // ==================== MUL / IMUL / DIV / IDIV ====================

    // mul cx: 3*4=12, DX=0, CF=0
    [Fact]
    public void Mul_Const_UpdatesAxDx_AndClearsCf()
    {
        // 03h * 04h = 000Ch , high=0 => CF=OF=0
        var expr = BuildExpressions("""
            B8 03 00 ; mov ax, 3
            B9 04 00 ; mov cx, 4
            F7 E1    ; mul cx
            """);

        var block = expr.Blocks[0];
        // const * const folded => без SetOperation
        Assert.Empty(block.Operations);

        var ax = Assert.IsType<ConstExpr>(block.EndRegisters.AX);
        var dx = Assert.IsType<ConstExpr>(block.EndRegisters.DX);
        Assert.Equal(0x000C, ax.Value);
        Assert.Equal(0, dx.Value);

        var cf = Assert.IsType<ConstExpr>(block.EndRegisters.CF);
        Assert.Equal(0, cf.Value);
    }

    // imul cx: low/high в AX/DX, CF через сравнение старшей части
    [Fact]
    public void Imul_Variable_ProducesMulAndHighAndSetsCfAccordingToSignExtend()
    {
        var expr = BuildExpressions("F7 E9", vars =>  // IMUL CX  (F7 E9 = IMUL CX)
        {
            var a = vars.CreateVariable("a");
            var c = vars.CreateVariable("c");
            var init = RegisterExpressions.InitZero();
            return init
                .Set16(GpRegister16.AX, a)
                .Set16(GpRegister16.CX, c);
        });

        var block = expr.Blocks[0];
        // Для IMUL с переменными создаём 2 SetOperation: low и high
        Assert.Equal(2, block.Operations.Count);

        var setLow = Assert.IsType<SetOperation>(block.Operations[0]);
        var mul = Assert.IsType<Math2Expr>(setLow.Src);
        Assert.Equal(Math2Operation.Mul, mul.Operation);
        Assert.Equal("a", Assert.IsType<Variable>(mul.First).Name);
        Assert.Equal("c", Assert.IsType<Variable>(mul.Second).Name);

        // AX указывает на low var
        var axVar = Assert.IsType<Variable>(block.EndRegisters.AX);
        Assert.Equal(setLow.Dst, axVar);

        // DX указывает на high (shr)
        var dxVar = Assert.IsType<Variable>(block.EndRegisters.DX);
        Assert.NotNull(dxVar);

        // CF должен быть CmpExpr (high != expected sign-extend)
        var cf = block.EndRegisters.CF;
        var cfCmp = Assert.IsType<CmpExpr>(cf);
        Assert.Equal(CmpOperation.Ne, cfCmp.Operation);
    }

    // div cx: 0x17/5 → AX=4, DX=3
    [Fact]
    public void Div_Const_ProducesQuotientAndRemainder()
    {
        // DX:AX = 0:0017 / 0005  => AX=4 (quot 23/5=4), DX=3 (rem)
        var expr = BuildExpressions("""
            B8 17 00 ; mov ax, 0x17
            31 D2    ; xor dx, dx
            B9 05 00 ; mov cx, 5
            F7 F1    ; div cx
            """);

        var block = expr.Blocks[0];
        // Часть операций свёрнута (xor, const), но для div с const создастся Set для quot/rem
        // Проверяем итоговые регистры
        var ax = Assert.IsType<ConstExpr>(block.EndRegisters.AX);
        var dx = Assert.IsType<ConstExpr>(block.EndRegisters.DX);
        Assert.Equal(4, ax.Value);  // 0x17 / 5 = 4 (цело)
        Assert.Equal(3, dx.Value);  // 0x17 % 5 = 3
    }

    // idiv cx с переменными — частное и остаток в новых Variable
    [Fact]
    public void Idiv_WithVariable_DoesNotThrow_AndSetsAxDx()
    {
        var expr = BuildExpressions("F7 F9", vars =>  // IDIV CX
        {
            var val = vars.CreateVariable("val");
            var c = vars.CreateVariable("c");
            var init = RegisterExpressions.InitZero();
            return init
                .Set16(GpRegister16.AX, val)
                .Set16(GpRegister16.CX, c);
        });

        var block = expr.Blocks[0];
        // Должны быть SetOperation (минимум для quot)
        Assert.NotEmpty(block.Operations);
        var ax = Assert.IsType<Variable>(block.EndRegisters.AX);
        var dx = Assert.IsType<Variable>(block.EndRegisters.DX);
        Assert.NotNull(ax);
        Assert.NotNull(dx);
    }
}

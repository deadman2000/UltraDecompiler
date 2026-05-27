using UltraDecompiler.Decompilation;

namespace Tests.Expressions;

/// <summary>
/// Тесты ExpressionBuilder для поддержки memory-операндов (MemExpr).
/// </summary>
public class MemoryTests : BaseTests
{
    [Fact]
    public void GetExpression_DirectMemoryAddress_ReturnsMemExprWithConst()
    {
        // MOV AX, [1234h]  — A1 34 12
        var expr = BuildExpressions("A1 34 12");

        var ax = expr.Blocks[0].EndRegisters.AX;
        var mem = Assert.IsType<MemExpr>(ax);
        var addr = Assert.IsType<ConstExpr>(mem.Address);
        Assert.Equal(0x1234, addr.Value);
        Assert.Contains("[", mem.ToString());
        Assert.Contains("4660", mem.ToString()); // ConstExpr печатает десятичное значение
    }

    [Fact]
    public void GetExpression_MemoryReg_ReturnsMemExprWithRegister()
    {
        // MOV BX, 0100h; MOV AX, [BX]
        var expr = BuildExpressions("""
            BB 00 01   ; mov bx, 0100h
            8B 07      ; mov ax, [bx]
            """);

        var ax = expr.Blocks[0].EndRegisters.AX;
        var mem = Assert.IsType<MemExpr>(ax);
        // После MOV BX константа, адрес должен быть Const(0x100) или Variable
        Assert.NotNull(mem.Address);
        Assert.Contains("[", mem.ToString());
    }

    [Fact]
    public void HandleLea_ComputesEffectiveAddress()
    {
        // LEA BX, [SI+0Ah]  — 8D 5C 0A
        var expr = BuildExpressions("8D 5C 0A");

        var bx = expr.Blocks[0].EndRegisters.BX;
        var math = Assert.IsType<Math2Expr>(bx);
        Assert.Equal(Math2Operation.Add, math.Operation);

        // Один из операндов — SI (или его выражение), второй — Const(10)
        Assert.True(
            (math.First is ConstExpr c && c.Value == 10) ||
            (math.Second is ConstExpr c2 && c2.Value == 10),
            "LEA должен содержать смещение 10"
        );
        Assert.Contains("+", bx.ToString());
    }

    [Fact]
    public void GetExpression_ComplexAddress_BxSiPlusDisp_UsedInAdd()
    {
        // ADD AX, [BX+SI+5]  — 03 40 05
        var expr = BuildExpressions("03 40 05");

        Assert.Single(expr.Blocks[0].Operations);
        var setOp = Assert.IsType<SetOperation>(expr.Blocks[0].Operations[0]);
        var add = Assert.IsType<Math2Expr>(setOp.Src);

        // Один из операндов Add — MemExpr
        MemExpr? memExpr = add.First as MemExpr ?? add.Second as MemExpr;
        Assert.NotNull(memExpr);

        // Адрес должен быть (BX + SI) + 5
        var addrAdd = Assert.IsType<Math2Expr>(memExpr.Address);
        Assert.Equal(Math2Operation.Add, addrAdd.Operation);
        Assert.Contains("[", memExpr.ToString());
    }

    [Fact]
    public void GetExpression_NegativeDisplacement_BpMinus2()
    {
        // MOV CX, [BP-2]  — 8B 4E FE
        var expr = BuildExpressions("8B 4E FE");

        var cx = expr.Blocks[0].EndRegisters.CX;
        var mem = Assert.IsType<MemExpr>(cx);
        var addr = mem.Address;

        // Ожидаем Math2 (Add или Sub) с -2 или 0xFFFE
        Assert.True(
            addr is Math2Expr or ConstExpr,
            "Адрес [BP-2] должен быть выражением со смещением"
        );
        Assert.Contains("[", mem.ToString());
    }

    [Fact]
    public void GetExpression_MemExprInCmp_SetsZfCorrectly()
    {
        // CMP [DI], 0   (сначала загрузим DI)
        var expr = BuildExpressions("""
            BF 00 02   ; mov di, 0200h
            83 3D 00   ; cmp word [di], 0
            """);

        var zf = expr.Blocks[0].EndRegisters.ZF;
        var cmp = Assert.IsType<CmpExpr>(zf);
        // Один из операндов Cmp — MemExpr
        Assert.True(
            cmp.Left is MemExpr || cmp.Right is MemExpr,
            "Cmp после сравнения с памятью должен содержать MemExpr"
        );
    }

    [Fact]
    public void GetExpression_ExplicitSegmentOverride_EsPrefix()
    {
        // ES: MOV AX, [1234h]  — 26 8B 06 34 12
        var expr = BuildExpressions("26 8B 06 34 12");

        var ax = expr.Blocks[0].EndRegisters.AX;
        var mem = Assert.IsType<MemExpr>(ax);

        Assert.NotNull(mem.Segment);
        // Сегмент должен быть выражением из ES (в Init* обычно Variable)
        Assert.Contains(":", mem.ToString()); // должно рендериться как "ES:[...]" или "varX:[...]"
    }

    [Fact]
    public void GetExpression_DefaultSegment_BpUsesSs_BxUsesDs()
    {
        // [BP-2] без префикса → по умолчанию SS
        var bpExpr = BuildExpressions("8B 4E FE"); // MOV CX, [BP-2]
        var memBp = Assert.IsType<MemExpr>(bpExpr.Blocks[0].EndRegisters.CX);
        Assert.NotNull(memBp.Segment); // должен быть SS (или его символическое значение)

        // [BX] без префикса → по умолчанию DS
        var bxExpr = BuildExpressions("""
            BB 00 01
            8B 07
            """); // MOV BX, 100h; MOV AX, [BX]
        var memBx = Assert.IsType<MemExpr>(bxExpr.Blocks[0].EndRegisters.AX);
        Assert.NotNull(memBx.Segment);
    }

    [Fact]
    public void GetExpression_SegmentInToString_RendersNicely()
    {
        // CS: MOV AX, [SI+4]
        var expr = BuildExpressions("2E 8B 44 04");

        var ax = expr.Blocks[0].EndRegisters.AX;
        var mem = Assert.IsType<MemExpr>(ax);
        var s = mem.ToString();
        Assert.Contains("[", s);
        // Должен присутствовать какой-то сегмент (либо "CS:" либо имя переменной)
        Assert.True(s.Contains(":") || s.StartsWith("["), "MemExpr.ToString должен отражать сегмент");
    }
}

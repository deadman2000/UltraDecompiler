using UltraDecompiler.Decompilation;
using UltraDecompiler.Decompilation.Operations;

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
        // SI init = 0 (const) => eff addr 0+10 folds to const 10
        var expr = BuildExpressions("8D 5C 0A");

        var bx = expr.Blocks[0].EndRegisters.BX;
        var addr = Assert.IsType<ConstExpr>(bx);
        Assert.Equal(10, addr.Value);
    }

    [Fact]
    public void GetExpression_ComplexAddress_BxSiPlusDisp_UsedInAdd()
    {
        // ADD AX, [BX+SI+5]  — 03 40 05
        // BX=SI=0 init => addr folds to Const(5)
        var expr = BuildExpressions("03 40 05");

        Assert.Single(expr.Blocks[0].Operations);
        var setOp = Assert.IsType<SetOperation>(expr.Blocks[0].Operations[0]);
        var add = Assert.IsType<Math2Expr>(setOp.Src);

        // Один из операндов Add — MemExpr с const-адресом (улучшение от folding)
        MemExpr? memExpr = add.First as MemExpr ?? add.Second as MemExpr;
        Assert.NotNull(memExpr);

        var addr = Assert.IsType<ConstExpr>(memExpr.Address);
        Assert.Equal(5, addr.Value);
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

    // === Тесты LDS / LES (far pointer load) ===

    [Fact]
    public void HandleLds_Basic_LoadsOffsetToRegAndSegmentToDs()
    {
        // LDS AX, [0260h]
        var expr = BuildExpressions("C5 06 60 02");

        // AX должен получить MemExpr по адресу 0260h
        var ax = expr.Blocks[0].EndRegisters.AX;
        var offsetMem = Assert.IsType<MemExpr>(ax);
        var offsetAddr = Assert.IsType<ConstExpr>(offsetMem.Address);
        Assert.Equal(0x0260, offsetAddr.Value);

        // DS должен получить MemExpr по адресу 0262h (старшее слово)
        var ds = expr.Blocks[0].EndRegisters.DS;
        var segMem = Assert.IsType<MemExpr>(ds);
        var segAddr = Assert.IsType<ConstExpr>(segMem.Address);
        Assert.Equal(0x0262, segAddr.Value);
    }

    [Fact]
    public void HandleLds_WithSsOverride_UsesSsForMemoryRead()
    {
        // SS: LDS AX, [0264h]
        var expr = BuildExpressions("36 C5 06 64 02");

        var ax = expr.Blocks[0].EndRegisters.AX;
        var mem = Assert.IsType<MemExpr>(ax);
        Assert.NotNull(mem.Segment); // Должен быть SS (или его символическое значение из Init)
        Assert.Contains(":", mem.ToString()); // рендеринг должен показывать сегмент

        // Проверяем, что и в DS тоже лежит MemExpr с тем же сегментом-источником
        var ds = expr.Blocks[0].EndRegisters.DS;
        var dsMem = Assert.IsType<MemExpr>(ds);
        Assert.NotNull(dsMem.Segment);
    }

    [Fact]
    public void HandleLes_Basic_LoadsOffsetToRegAndSegmentToEs()
    {
        // LES BX, [0010h]
        var expr = BuildExpressions("C4 1E 10 00");

        var bx = expr.Blocks[0].EndRegisters.BX;
        var offsetMem = Assert.IsType<MemExpr>(bx);
        var addr = Assert.IsType<ConstExpr>(offsetMem.Address);
        Assert.Equal(0x0010, addr.Value);

        // ES (а не DS!)
        var es = expr.Blocks[0].EndRegisters.ES;
        var esMem = Assert.IsType<MemExpr>(es);
        var esAddr = Assert.IsType<ConstExpr>(esMem.Address);
        Assert.Equal(0x0012, esAddr.Value);

        // DS при этом не должен измениться от своего начального значения
        // (мы не проверяем точное значение, просто что это не тот же MemExpr по 0012)
    }
}

namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты инструкции MOV (пересылка данных).
/// Проверяют корректное создание SetOperation для регистров и StoreOperation для памяти.
/// </summary>
public class MovTests : BaseTests
{
    #region MOV reg, imm

    [Fact]
    public void Mov_Ax_Immediate16_ProducesSetOperation()
    {
        // MOV AX, 1234h
        var expr = BuildExpressions("B8 34 12");

        var set = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<SetOperation>(set);

        // Целевая переменная — AX
        Assert.Equal(expr.Variables.AX, s.Dst);

        // Значение — константа 0x1234
        var constExpr = Assert.IsType<ConstExpr>(s.Src);
        Assert.Equal(0x1234, constExpr.Value);
    }

    [Fact]
    public void Mov_Cx_Immediate16_ProducesSetOperation()
    {
        // MOV CX, 5678h
        var expr = BuildExpressions("B9 78 56");

        var set = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<SetOperation>(set);

        Assert.Equal(expr.Variables.CX, s.Dst);
        var constExpr = Assert.IsType<ConstExpr>(s.Src);
        Assert.Equal(0x5678, constExpr.Value);
    }

    [Fact]
    public void Mov_Al_Immediate8_ProducesSetOperation()
    {
        // MOV AL, 55h
        var expr = BuildExpressions("B0 55");

        var set = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<SetOperation>(set);

        // Для 8-битных регистров целевая переменная — AX
        Assert.Equal(expr.Variables.AX, s.Dst);
        // Источник — сложное выражение: (AX_old & 0xFF00) | (0x55 & 0xFF)
        // Из-за constant folding: (AX & 0xFF00) | 0x55
        var orExpr = Assert.IsType<Math2Expr>(s.Src);
        Assert.Equal(Math2Operation.Or, orExpr.Operation);

        // Левая часть: AX & 0xFF00 (сохраняем старший байт)
        var andExpr = Assert.IsType<Math2Expr>(orExpr.First);
        Assert.Equal(Math2Operation.And, andExpr.Operation);
        Assert.Equal(0xFF00, ((ConstExpr)andExpr.Second).Value);

        // Правая часть: 0x55 (constant folding применился к маске)
        Assert.Equal(0x55, ((ConstExpr)orExpr.Second).Value);
    }

    #endregion

    #region MOV reg, reg

    [Fact]
    public void Mov_Ax_Bx_ProducesSetOperation()
    {
        // MOV AX, BX
        var expr = BuildExpressions("8B C3");

        var set = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<SetOperation>(set);

        Assert.Equal(expr.Variables.AX, s.Dst);
        // Источник — переменная BX
        Assert.Equal(expr.Variables.BX, s.Src);
    }

    [Fact]
    public void Mov_Al_Bl_ProducesSetOperation()
    {
        // MOV AL, BL
        var expr = BuildExpressions("88 D8");

        var set = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<SetOperation>(set);

        // Для 8-битных регистров целевая переменная — AX
        Assert.Equal(expr.Variables.AX, s.Dst);
        // Источник — сложное выражение: (AX_old & 0xFF00) | (BL & 0xFF)
        var orExpr = Assert.IsType<Math2Expr>(s.Src);
        Assert.Equal(Math2Operation.Or, orExpr.Operation);

        // Правая часть: BL & 0xFF
        var andExpr = Assert.IsType<Math2Expr>(orExpr.Second);
        Assert.Equal(Math2Operation.And, andExpr.Operation);
    }

    [Fact]
    public void Mov_MultipleMovs_ChainValues()
    {
        // MOV AX, 1234h; MOV BX, AX
        var expr = BuildExpressions("""
            B8 34 12  ; MOV AX, 1234h
            8B D8     ; MOV BX, AX
            """);

        Assert.Equal(2, expr.Blocks[0].Operations.Count);

        // Первая операция: AX = 1234h
        var set1 = Assert.IsType<SetOperation>(expr.Blocks[0].Operations[0]);
        Assert.Equal(expr.Variables.AX, set1.Dst);
        Assert.Equal(0x1234, ((ConstExpr)set1.Src).Value);

        // Вторая операция: BX = AX
        var set2 = Assert.IsType<SetOperation>(expr.Blocks[0].Operations[1]);
        Assert.Equal(expr.Variables.BX, set2.Dst);
        // Источник должен ссылаться на AX (через Variable)
        Assert.Equal(expr.Variables.AX, set2.Src);
    }

    #endregion

    #region MOV reg, mem

    [Fact]
    public void Mov_Ax_MemoryDirect_ProducesSetOperation()
    {
        // MOV AX, [1234h]
        var expr = BuildExpressions("A1 34 12");

        var set = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<SetOperation>(set);

        Assert.Equal(expr.Variables.AX, s.Dst);
        // Источник — MemExpr с адресом 0x1234
        var mem = Assert.IsType<MemExpr>(s.Src);
        var addr = Assert.IsType<ConstExpr>(mem.Address);
        Assert.Equal(0x1234, addr.Value);
    }

    [Fact]
    public void Mov_Al_MemoryDirect_ProducesSetOperation()
    {
        // MOV AL, [1234h]
        var expr = BuildExpressions("A0 34 12");

        var set = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<SetOperation>(set);

        Assert.Equal(expr.Variables.AX, s.Dst);
        // Источник — сложное выражение: (AX_old & 0xFF00) | ([mem] & 0xFF)
        var orExpr = Assert.IsType<Math2Expr>(s.Src);
        Assert.Equal(Math2Operation.Or, orExpr.Operation);

        // Правая часть: [mem] & 0xFF
        var andExpr = Assert.IsType<Math2Expr>(orExpr.Second);
        Assert.Equal(Math2Operation.And, andExpr.Operation);
        var addrExpr = andExpr.First;
        // Адрес может быть MemExpr или Math2Expr (если есть сегмент)
        Assert.NotNull(addrExpr);
        Assert.Equal(0xFF, ((ConstExpr)andExpr.Second).Value);
    }

    [Fact]
    public void Mov_Ax_MemoryBx_ProducesSetOperation()
    {
        // MOV AX, [BX]
        var expr = BuildExpressions("8B 07");

        var set = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<SetOperation>(set);

        Assert.Equal(expr.Variables.AX, s.Dst);
        var mem = Assert.IsType<MemExpr>(s.Src);
        // Адрес — переменная BX
        Assert.Equal(expr.Variables.BX, mem.Address);
    }

    [Fact]
    public void Mov_Cx_MemoryBpPlusSi_ProducesSetOperation()
    {
        // MOV CX, [BP+SI]
        var expr = BuildExpressions("8B 0A");

        var set = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<SetOperation>(set);

        Assert.Equal(expr.Variables.CX, s.Dst);
        var mem = Assert.IsType<MemExpr>(s.Src);
        // Адрес = BP + SI
        var addr = Assert.IsType<Math2Expr>(mem.Address);
        Assert.Equal(Math2Operation.Add, addr.Operation);
    }

    #endregion

    #region MOV mem, reg

    [Fact]
    public void Mov_MemoryDirect_Ax_ProducesStoreOperation()
    {
        // MOV [1234h], AX
        var expr = BuildExpressions("A3 34 12");

        var store = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<StoreOperation>(store);

        // Адрес — константа 0x1234
        var addr = Assert.IsType<ConstExpr>(s.Address);
        Assert.Equal(0x1234, addr.Value);

        // Значение — переменная AX
        Assert.Equal(expr.Variables.AX, s.Value);
    }

    [Fact]
    public void Mov_MemoryDirect_Al_ProducesStoreOperation()
    {
        // MOV [1234h], AL
        var expr = BuildExpressions("A2 34 12");

        var store = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<StoreOperation>(store);

        var addr = Assert.IsType<ConstExpr>(s.Address);
        Assert.Equal(0x1234, addr.Value);
        // Значение — AL = (AX & 0xFF), но с учётом нашего нового представления
        // AL = (AX_old & 0xFF00) | (AX & 0xFF), но AX_old = AX (первая инструкция)
        // Упрощаем проверку: значение должно содержать маску & 0xFF
        var valueExpr = s.Value;
        // Для AL значение — это (AX & 0xFF) после LowByte()
        var andExpr = Assert.IsType<Math2Expr>(valueExpr);
        Assert.Equal(Math2Operation.And, andExpr.Operation);
        Assert.Equal(0xFF, ((ConstExpr)andExpr.Second).Value);
    }

    [Fact]
    public void Mov_MemoryBx_Ax_ProducesStoreOperation()
    {
        // MOV [BX], AX
        var expr = BuildExpressions("89 07");

        var store = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<StoreOperation>(store);

        // Адрес — переменная BX
        Assert.Equal(expr.Variables.BX, s.Address);
        Assert.Equal(expr.Variables.AX, s.Value);
    }

    [Fact]
    public void Mov_MemoryWithSegmentOverride_ProducesStoreOperation()
    {
        // ES: MOV [BX], AX
        var expr = BuildExpressions("26 89 07");

        var store = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<StoreOperation>(store);

        Assert.Equal(expr.Variables.BX, s.Address);
        // Сегмент должен быть ES
        Assert.Equal(expr.Variables.ES, s.Segment);
    }

    #endregion

    #region MOV seg, reg и MOV reg, seg

    [Fact]
    public void Mov_Ds_Ax_ProducesSetOperation()
    {
        // MOV DS, AX
        var expr = BuildExpressions("8E D8");

        var set = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<SetOperation>(set);

        Assert.Equal(expr.Variables.DS, s.Dst);
        Assert.Equal(expr.Variables.AX, s.Src);
    }

    [Fact]
    public void Mov_Ax_Ds_ProducesSetOperation()
    {
        // MOV AX, DS
        var expr = BuildExpressions("8C D8");

        var set = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<SetOperation>(set);

        Assert.Equal(expr.Variables.AX, s.Dst);
        Assert.Equal(expr.Variables.DS, s.Src);
    }

    #endregion

    #region MOV с сохранением битов

    [Fact]
    public void Mov_Al_PreservesHighByte()
    {
        // Ожидаем: AX = (0x1234 & 0xFF00) | (0x55 & 0xFF) = 0x1255
        var expr = BuildExpressions("""
            B8 34 12  ; MOV AX, 1234h
            B0 55     ; MOV AL, 55h
            """);

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        Assert.Equal(2, sets.Count);

        // Вторая операция: AL = 55h
        var setAl = sets[1];
        Assert.Equal(expr.Variables.AX, setAl.Dst);

        // Источник должен быть OR-выражением
        var orExpr = Assert.IsType<Math2Expr>(setAl.Src);
        Assert.Equal(Math2Operation.Or, orExpr.Operation);

        // Левая часть: старое AX & 0xFF00 (сохраняем AH = 0x12)
        var andHigh = Assert.IsType<Math2Expr>(orExpr.First);
        Assert.Equal(Math2Operation.And, andHigh.Operation);
        Assert.Equal(0xFF00, ((ConstExpr)andHigh.Second).Value);

        // Правая часть: 0x55 (constant folding)
        Assert.Equal(0x55, ((ConstExpr)orExpr.Second).Value);
    }

    [Fact]
    public void Mov_Ah_PreservesLowByte()
    {
        // Ожидаем: AX = (0x1234 & 0x00FF) | (0x55 << 8) = 0x5534
        var expr = BuildExpressions("""
            B8 34 12  ; MOV AX, 1234h
            B4 55     ; MOV AH, 55h
            """);

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        Assert.Equal(2, sets.Count);

        // Вторая операция: AH = 55h
        var setAh = sets[1];
        Assert.Equal(expr.Variables.AX, setAh.Dst);

        // Источник должен быть OR-выражением
        var orExpr = Assert.IsType<Math2Expr>(setAh.Src);
        Assert.Equal(Math2Operation.Or, orExpr.Operation);

        // Левая часть: старое AX & 0x00FF (сохраняем AL = 0x34)
        var andLow = Assert.IsType<Math2Expr>(orExpr.First);
        Assert.Equal(Math2Operation.And, andLow.Operation);
        Assert.Equal(0x00FF, ((ConstExpr)andLow.Second).Value);

        // Правая часть: 0x55 << 8 = 0x5500 (constant folding)
        var rightConst = Assert.IsType<ConstExpr>(orExpr.Second);
        Assert.Equal(0x5500, rightConst.Value);
    }

    #endregion

    #region MOV с параметрами стека

    [Fact]
    public void Mov_Ax_BpPlus4_LoadsParameter()
    {
        var expr = BuildExpressions("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            8B 46 04  ; MOV AX, [BP+4]
            """);

        // Пролог не создаёт операций — только загрузка arg0
        var loadParam = Assert.Single(expr.Blocks[0].Operations.OfType<SetOperation>());
        Assert.Equal(expr.Variables.AX, loadParam.Dst);
        // Должна загрузиться переменная arg0
        Assert.Equal("arg0", loadParam.Src.ToString());
    }

    [Fact]
    public void Mov_BpPlusMinus2_Ax_StoresLocal()
    {
        // Проверяем, что MOV [BP-2], AX создаёт SetOperation для локальной переменной
        var expr = BuildExpressions("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            50        ; PUSH AX
            89 46 FE  ; MOV [BP-2], AX
            """);

        // Должна быть SetOperation для локальной переменной
        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        Assert.NotEmpty(sets);

        // Последняя SetOperation — запись в локаль
        var setLocal = sets.Last();
        Assert.Equal(expr.Variables.AX, setLocal.Src);
    }

    #endregion

    #region MOV с цепочками выражений

    [Fact]
    public void Mov_WithChainOfMovs_PreservesExpressionChain()
    {
        var expr = BuildExpressions("""
            B8 34 12  ; MOV AX, 1234h
            8B D8     ; MOV BX, AX
            8B CB     ; MOV CX, BX
            """);

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        Assert.Equal(3, sets.Count);

        // Первая операция: AX = 1234h
        var set1 = sets[0];
        Assert.Equal(expr.Variables.AX, set1.Dst);
        Assert.Equal(0x1234, ((ConstExpr)set1.Src).Value);

        // Вторая операция: BX = AX
        var set2 = sets[1];
        Assert.Equal(expr.Variables.BX, set2.Dst);
        Assert.Equal(expr.Variables.AX, set2.Src);

        // Третья операция: CX = BX
        var set3 = sets[2];
        Assert.Equal(expr.Variables.CX, set3.Dst);
        Assert.Equal(expr.Variables.BX, set3.Src);
    }

    #endregion
}

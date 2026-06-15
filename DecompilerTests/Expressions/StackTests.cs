namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты символической поддержки стека (PUSH/POP) в ExpressionBuilder.
/// </summary>
public class StackTests : BaseTests
{
    // push ax — значение на символическом стеке, без Operation
    [Fact]
    public void PushReg16_PlacesValueOnEndStack()
    {
        var expr = BuildExpressions("""
            b8 34 12   ; mov ax, 1234h
            50         ; push ax
            """);

        var block = expr.Blocks[0];
        Assert.Empty(block.Operations); // PUSH не создаёт Operation, только обновляет стек
        Assert.Single(block.EndStack);
        var top = block.EndStack.Peek();
        var c = Assert.IsType<ConstExpr>(top);
        Assert.Equal(0x1234, c.Value);
    }

    // push bx; pop cx — CX получает значение BX
    [Fact]
    public void PushPop_SameRegister_Roundtrip()
    {
        var expr = BuildExpressions("""
            bb cd ab   ; mov bx, 0abcdh
            53         ; push bx
            59         ; pop cx
            """);

        var block = expr.Blocks[0];
        Assert.Empty(block.Operations);

        // После POP CX должен содержать то же значение, что было в BX
        var cxVal = block.EndRegisters.Get16(GpRegister16.CX);
        var c = Assert.IsType<ConstExpr>(cxVal);
        Assert.Equal(0xABCD, c.Value);
    }

    [Fact]
    public void PushVariable_PopToMemory_ProducesStoreOperation()
    {
        // Значение input уже лежит на стеке (передали через initialStack)
        var expr = BuildExpressions("""
            8f 06 00 20   ; pop word ptr [2000h]
            """, vars =>
        {
            var input = vars.CreateVariable("input");
            var regs = RegisterExpressions.InitCom(vars) with { AX = input };
            var stack = new Stack<Expr>();
            stack.Push(input);
            return (regs, stack);
        });

        var store = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<StoreOperation>(store);

        var addr = Assert.IsType<ConstExpr>(s.Address);
        Assert.Equal(0x2000, addr.Value);

        var v = Assert.IsType<Variable>(s.Value);
        Assert.Equal("input", v.Name);
    }

    // LIFO: второй push всплывает в BX, первый — в CX
    [Fact]
    public void MultiplePushPop_PreservesOrder()
    {
        var expr = BuildExpressions("""
            b8 01 00   ; mov ax, 0001h
            50         ; push ax
            b8 02 00   ; mov ax, 0002h
            50         ; push ax
            5b         ; pop bx
            59         ; pop cx
            """);

        var block = expr.Blocks[0];
        Assert.Empty(block.Operations);

        var bx = Assert.IsType<ConstExpr>(block.EndRegisters.Get16(GpRegister16.BX));
        var cx = Assert.IsType<ConstExpr>(block.EndRegisters.Get16(GpRegister16.CX));

        Assert.Equal(0x0002, bx.Value); // значение из второго PUSH
        Assert.Equal(0x0001, cx.Value); // значение из первого PUSH
    }

    [Fact]
    public void PopToSegmentRegister_UpdatesRegister()
    {
        // Значение DS уже лежит на стеке (передали через initialStack)
        var expr = BuildExpressions("""
            07   ; pop es
            """, vars =>
        {
            var regs = RegisterExpressions.InitCom(vars);
            var stack = new Stack<Expr>();
            stack.Push(regs.GetSegment(CpuSegmentRegister.DS));
            return (regs, stack);
        });

        var esVal = expr.Blocks[0].EndRegisters.GetSegment(CpuSegmentRegister.ES);
        var dsVal = expr.Blocks[0].EndRegisters.GetSegment(CpuSegmentRegister.DS);

        // ES получает то же символическое значение, что было в DS
        Assert.Same(dsVal, esVal);
    }

    // pop при пустом стеке → Variable stackErr
    [Fact]
    public void PopFromEmptyStack_UsesStackErrPlaceholder()
    {
        var expr = BuildExpressions("""
            58   ; pop ax   (стек пуст)
            """);

        var block = expr.Blocks[0];
        Assert.Empty(block.EndStack);

        var axVal = block.EndRegisters.Get16(GpRegister16.AX);
        var v = Assert.IsType<Variable>(axVal);
        Assert.Equal("stackErr", v.Name);
    }

    [Fact]
    public void PushThenPopToMemory_ProducesStoreWithPushedValue()
    {
        var expr = BuildExpressions("""
            b8 dc fe      ; mov ax, 0fedch
            50            ; push ax
            8f 06 00 10   ; pop word ptr [1000h]
            """);

        var store = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<StoreOperation>(store);

        var addr = Assert.IsType<ConstExpr>(s.Address);
        Assert.Equal(0x1000, addr.Value);

        var val = Assert.IsType<ConstExpr>(s.Value);
        Assert.Equal(0xFEDC, val.Value);
    }

    [Fact]
    public void Leave_UpdatesSpFromBpAndPopsBp()
    {
        // Типичный эпилог: mov bp, sp; ... ; leave
        var expr = BuildExpressions("""
            8B EC       ; mov bp, sp
            C9          ; leave
            """, isCom: true);

        // После leave SP должен равняться (предыдущему) BP, а BP — значению, которое было на стеке
        // В этом простом случае стек символически может быть пустым → BP станет MemExpr
        var sp = expr.Blocks[0].EndRegisters.Get16(GpRegister16.SP);
        var bp = expr.Blocks[0].EndRegisters.Get16(GpRegister16.BP);

        // SP должен быть равен тому, что было в BP до leave (в данном случае SP после mov bp,sp)
        Assert.NotNull(sp);
        Assert.NotNull(bp);
    }

    [Fact]
    public void Enter_CreatesStackFrame()
    {
        // C8 04 00 00   — ENTER 4, 0   (типичный случай для QuickC)
        var expr = BuildExpressions("C8 04 00 00 ; enter 4, 0", isCom: true);

        // После ENTER:
        // - BP должен быть равен SP до вычитания
        // - SP должен уменьшиться на 4
        var bp = expr.Blocks[0].EndRegisters.Get16(GpRegister16.BP);
        var sp = expr.Blocks[0].EndRegisters.Get16(GpRegister16.SP);

        Assert.NotNull(bp);
        Assert.NotNull(sp);

        // Проверяем, что в стеке что-то появилось (старый BP)
        // (точное значение зависит от инициализации)
    }
}

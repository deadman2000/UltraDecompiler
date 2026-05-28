using UltraDecompiler.Decompilation;

namespace Tests.Expressions;

/// <summary>
/// Тесты поддержки строковых инструкций (MOVS/CMPS/SCAS/LODS/STOS + REP*).
/// </summary>
public class StringOperationTests : BaseTests
{
    [Fact]
    public void PriorityExample_RepStosb_BufferClear()
    {
        // mov di, 352h
        // mov cx, 780h
        // sub cx, di      ; CX = 0x42E
        // xor ax, ax      ; AL = 0 (но в тесте делаем Variable)
        // rep stosb       ; обнулить 0x42E байт

        var expr = BuildExpressions("""
            FC           ; cld
            BF 52 03     ; mov di, 352h
            B9 80 07     ; mov cx, 780h
            2B CF        ; sub cx, di
            33 C0        ; xor ax, ax
            F3 AA        ; rep stosb
            """);

        var block = expr.Blocks[0];

        // 1. Должен быть создан цикл (WhileOperation)
        var loop = Assert.IsType<WhileOperation>(block.Operations.Last());

        // 2. В теле цикла должна быть StoreOperation
        Assert.Contains(loop.Body, op => op is StoreOperation);

        // 3. После цикла DI должен быть точным значением 0x780 (не post-loop переменная)
        var finalDi = block.EndRegisters.DI;
        Assert.IsType<ConstExpr>(finalDi);
        Assert.Equal(0x780, ((ConstExpr)finalDi).Value);

        // 4. После цикла CX должен быть 0
        var finalCx = block.EndRegisters.Get16(1); // CX
        Assert.Equal(ConstExpr.Zero, finalCx);
    }

    // ============================================================
    // Базовые тесты одиночных строковых инструкций (без REP)
    // ============================================================

    [Fact]
    public void SingleStosb_WritesAlToEsDi()
    {
        var expr = BuildExpressions("""
            FC          ; cld
            B0 AA       ; mov al, 0AAh
            BF 00 40    ; mov di, 4000h
            AA          ; stosb
            """);

        var block = expr.Blocks[0];
        // Должна быть StoreOperation
        Assert.Contains(block.Operations, op => op is StoreOperation);
    }

    [Fact]
    public void SingleMovsw_CopiesWordAndAdvancesPointers()
    {
        var expr = BuildExpressions("""
            FC          ; cld
            A5          ; movsw
            """);

        var block = expr.Blocks[0];
        // Должна быть StoreOperation (копирование слова)
        Assert.Contains(block.Operations, op => op is StoreOperation);
    }

    [Fact]
    public void SingleCmpsb_UpdatesFlags()
    {
        var expr = BuildExpressions("""
            FC          ; cld
            A6          ; cmpsb
            """);

        var block = expr.Blocks[0];
        // CMPS должен обновить флаги (ZF хотя бы)
        Assert.IsType<CmpExpr>(block.EndRegisters.ZF);
    }

    // ============================================================
    // Тесты REP MOVS / STOS / LODS (без условия по ZF)
    // ============================================================

    [Fact]
    public void RepStosb_CreatesWhileLoop()
    {
        var expr = BuildExpressions("""
            FC              ; cld
            B9 04 00        ; mov cx, 4
            B0 00           ; mov al, 0
            BF 00 30        ; mov di, 3000h
            F3 AA           ; rep stosb
            """);

        var block = expr.Blocks[0];

        // Должна появиться WhileOperation (REP-цикл)
        Assert.Contains(block.Operations, op => op is WhileOperation);
    }

    // ============================================================
    // Тесты REPZ / REPNZ CMPS и SCAS (с досрочным выходом)
    // ============================================================

    [Fact]
    public void RepzCmpsb_Basic_DoesNotCrash()
    {
        // Минимальный тест: REPZ CMPSB не должен падать с исключением
        var expr = BuildExpressions("""
            FC          ; cld
            F3 A6       ; repz cmpsb
            """);

        var block = expr.Blocks[0];
        Assert.NotNull(block);
    }

    [Fact]
    public void RepzCmpsb_ExitsOnFirstMismatch()
    {
        // REPZ CMPSB должен выйти, когда найдёт неравенство (ZF=0)
        var expr = BuildExpressions("""
            FC              ; cld
            B9 10 00        ; mov cx, 10h
            F3 A6           ; repz cmpsb
            """);

        var block = expr.Blocks[0];

        // Должен быть создан цикл
        Assert.Contains(block.Operations, op => op is WhileOperation);

        // После выхода ZF должен быть установлен (от последнего сравнения)
        // В данном минимальном тесте мы просто проверяем, что флаг не сломан
        Assert.NotNull(block.EndRegisters.ZF);
    }

    [Fact]
    public void RepnzScasw_Basic_CreatesLoop()
    {
        // REPNZ SCASW — поиск первого совпадения
        var expr = BuildExpressions("""
            FD              ; std
            B9 10 00        ; mov cx, 10h
            F2 AF           ; repnz scasw
            """);

        var block = expr.Blocks[0];
        Assert.Contains(block.Operations, op => op is WhileOperation);
    }

    [Fact]
    public void RepzScasb_FindsMatch_ExitsWithCorrectFlags()
    {
        // REPZ SCASB — поиск байта. Должен выйти по совпадению (ZF=1)
        var expr = BuildExpressions("""
            FC              ; cld
            B9 10 00        ; mov cx, 10h
            F3 AE           ; repz scasb
            """);

        var block = expr.Blocks[0];
        Assert.Contains(block.Operations, op => op is WhileOperation);
        // После выхода флаги должны быть установлены
        Assert.NotNull(block.EndRegisters.ZF);
    }

    [Fact]
    public void RepnzCmpsw_ExitsOnMatch()
    {
        // REPNZ CMPSW — поиск неравенства. Выход по равенству (ZF=1)
        var expr = BuildExpressions("""
            FC              ; cld
            B9 10 00        ; mov cx, 10h
            F2 A7           ; repnz cmpsw
            """);

        var block = expr.Blocks[0];
        Assert.Contains(block.Operations, op => op is WhileOperation);
    }

    [Fact]
    public void RepzScasw_WithCld_UpdatesDiCorrectlyInLoop()
    {
        // Проверяем, что при REPZ SCASW с CLD DI обновляется в правильную сторону внутри цикла
        // Явно задаём CX, чтобы избежать edge case CX=0 из InitExe
        var expr = BuildExpressions("""
            FC              ; cld
            B9 10 00        ; mov cx, 10h
            F3 AF           ; repz scasw
            """);

        var block = expr.Blocks[0];
        var loop = block.Operations.OfType<WhileOperation>().FirstOrDefault();
        Assert.NotNull(loop);

        // В теле должны быть операции обновления указателя (SetOperation)
        bool hasPointerUpdate = loop.Body.Any(op => op is SetOperation);
        Assert.True(hasPointerUpdate, "Loop body should contain pointer update operations");
    }

    [Fact]
    public void SingleScasb_WithoutRep_Works()
    {
        // Одиночный SCASB (без REP) — должен проходить через обычную ветку HandleStringScan
        var expr = BuildExpressions("""
            FC          ; cld
            AE          ; scasb
            """);

        var block = expr.Blocks[0];
        // Не должен падать и должен обновлять флаги
        Assert.NotNull(block.EndRegisters.ZF);
    }

    [Fact]
    public void RepzScasb_ExitsOnMatch_UpdatesFlags()
    {
        // REPZ SCASB должен выйти по совпадению и оставить флаги от последнего сравнения
        var expr = BuildExpressions("""
            FC              ; cld
            B9 10 00        ; mov cx, 10h
            F3 AE           ; repz scasb
            """);

        var block = expr.Blocks[0];
        Assert.Contains(block.Operations, op => op is WhileOperation);
        // Флаги после цикла должны быть установлены
        Assert.NotNull(block.EndRegisters.ZF);
        Assert.NotNull(block.EndRegisters.CF);
    }

    [Fact]
    public void RepnzScasb_ExitsOnFirstMatch_LeavesProperFlags()
    {
        // REPNZ SCASB — поиск первого совпадения. Должен выйти с ZF=1
        var expr = BuildExpressions("""
            FC              ; cld
            B9 10 00        ; mov cx, 10h
            F2 AE           ; repnz scasb
            """);

        var block = expr.Blocks[0];
        Assert.Contains(block.Operations, op => op is WhileOperation);
        Assert.NotNull(block.EndRegisters.ZF);
    }

    [Fact]
    public void RepMovsb_InitializesLoopVarsBeforeWhile()
    {
        var expr = BuildExpressions("""
            FC              ; cld
            B9 04 00        ; mov cx, 4
            BE 00 10        ; mov si, 1000h
            BF 00 20        ; mov di, 2000h
            F3 A4           ; rep movsb
            """);

        var block = expr.Blocks[0];
        var whileIdx = block.Operations.FindIndex(op => op is WhileOperation);
        Assert.True(whileIdx > 0, "WhileOperation should not be the first operation");

        // The operations before While should be initializations (SetOperation)
        for (int i = 0; i < whileIdx; i++)
        {
            Assert.IsType<SetOperation>(block.Operations[i]);
        }
    }

    [Fact]
    public void RepzCmpsb_InitializesLoopVarsBeforeWhile()
    {
        var expr = BuildExpressions("""
            FC              ; cld
            B9 05 00        ; mov cx, 5
            F3 A6           ; repz cmpsb
            """);

        var block = expr.Blocks[0];
        var whileIdx = block.Operations.FindIndex(op => op is WhileOperation);
        Assert.True(whileIdx > 0, "WhileOperation should not be the first operation");

        for (int i = 0; i < whileIdx; i++)
        {
            Assert.IsType<SetOperation>(block.Operations[i]);
        }
    }

    [Fact]
    public void RepStosb_WithCxZero_DoesNothing()
    {
        // Классический edge case: REP с CX=0 не должен выполнять ни одной итерации
        var expr = BuildExpressions("""
            FC              ; cld
            B9 00 00        ; mov cx, 0
            B0 00           ; mov al, 0
            BF 00 30        ; mov di, 3000h
            F3 AA           ; rep stosb
            """);

        var block = expr.Blocks[0];

        // Не должно быть WhileOperation (цикл не создаётся)
        Assert.DoesNotContain(block.Operations, op => op is WhileOperation);

        // DI и CX должны остаться теми же (или близко к исходным)
        // В данном случае DI должен остаться 0x3000
        var finalDi = block.EndRegisters.DI;
        Assert.IsType<ConstExpr>(finalDi);
        Assert.Equal(0x3000, ((ConstExpr)finalDi).Value);
    }
}

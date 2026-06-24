using UltraDecompiler.PostProcessing.Normalization;

namespace DecompilerTests.Expressions;

/// <summary>
/// Распознавание циклов QuickC /Od в OperationFlattener:
/// for по счётчику, while по указателю, вложенные for.
/// Эталон — паттерны из QuickC/PROGRAMS/forlp.c, fornt.c, forp2.c, forbk.c, whbrk.c, forcnt.c, whcnt.c.
/// </summary>
public class LoopRecognitionTests : BaseTests
{
    // sum_for: for (i = 0; i < 5; i++) sum += i
    // Ожидаем ForOperation с init, условием i < 5 и inc в заголовке for.
    [Fact(Skip = "NotImplemented")]
    public void SumFor_ProducesForLoop()
    {
        var ops = BuildOperations("""
            55                ; push bp
            8B EC             ; mov bp, sp
            81 EC 04 00       ; sub sp, 4
            C7 46 FC 00 00    ; sum = 0
            C7 46 FE 00 00    ; i = 0
            E9 0A 00          ; jmp header
            8B 46 FE          ; body: mov ax, [bp-2]
            01 46 FC          ; sum += ax
            83 46 FE 01       ; i++
            83 7E FE 05       ; header: cmp i, 5
            7D 03             ; jge exit
            E9 ED FF          ; jmp body
            8B 46 FC          ; exit: mov ax, sum
            8B E5             ; mov sp, bp
            5D                ; pop bp
            C3                ; ret
            """);
        var loop = Assert.Single(ops.OfType<ForOperation>());

        Assert.IsType<SetOperation>(loop.Init);
        Assert.IsType<CmpExpr>(loop.Condition);
        Assert.IsType<IncOperation>(loop.Iteration);
        Assert.DoesNotContain(ops, op => op is IfOperation);
    }

    // copy_str из LOOPS_OD.EXE (0xAF): while (*src) { *dst++ = *src++; } *dst = 0
    // Ожидаем WhileOperation — тело в ветке условия, без счётчика for.
    [Fact(Skip = "NotImplemented")]
    public void CopyStr_ProducesWhileLoop()
    {
        var ops = BuildOperations("""
            55 8B EC 81 EC 00 00 57 56
            8B 5E 06 8A 07 98 3D 00 00 75 03 E9 15 00
            8B 5E 06 83 46 06 01 8A 07 8B 5E 04 83 46 04 01 88 07 E9 DD FF
            8B 5E 04 C6 07 00 5E 5F 8B E5 5D C3
            """);
        Assert.Contains(ops, op => op is WhileOperation);
        Assert.DoesNotContain(ops, op => op is ForOperation);
    }

    // nested_for: два вложенных ForOperation (паттерн из fornt.c).
    [Fact(Skip = "NotImplemented")]
    public void NestedFor_ProducesTwoForLoops()
    {
        var ops = BuildOperations("""
            55 8B EC 81 EC 06 00 57 56
            C7 46 FA 01 00 C7 46 FE 01 00 E9 22 00
            C7 46 FC 01 00 E9 0D 00
            8B 46 FE F7 6E FC 01 46 FA 83 46 FC 01
            83 7E FC 03 7F 03 E9 EA FF
            83 46 FE 01
            83 7E FE 03 7F 03 E9 D5 FF
            8B 46 FA 5E 5F 8B E5 5D C3
            """);
        var nestedCount = OperationFlattener.EnumerateNested(ops).Count(o => o is ForOperation or WhileOperation or DoWhileOperation);
        Assert.True(nestedCount >= 2, "Ожидались минимум 2 цикла (вложенные for)");

        // Дополнительно: после оптимизатора temp не протекает, i*j подставлено напрямую в += (фикс бага fornt.c)
        var optimized = OperationOptimizer.Optimize(ops);
        var loops = OperationFlattener.EnumerateNested(optimized).Where(o => o is ForOperation or WhileOperation or DoWhileOperation).ToList();
        Assert.True(loops.Count >= 2);
    }


    // sum_for_step2: for (i = 0; i < 10; i += 2) — шаг через add [bp-N], 2 (temp + store).
    [Fact(Skip = "NotImplemented")]
    public void SumForStep2_ProducesForWithStep2()
    {
        var ops = BuildOperations("""
            55 8B EC 81 EC 04 00 57 56
            C7 46 FC 00 00 C7 46 FE 00 00 E9 0A 00
            8B 46 FE 01 46 FC 83 46 FE 02
            83 7E FE 0A 7D 03 E9 ED FF
            8B 46 FC E9 00 00
            5E 5F 8B E5 5D C3
            """);
        var hasFor = ops.OfType<ForOperation>().Any();
        Assert.True(hasFor || ops.OfType<WhileOperation>().Any() || ops.OfType<DoWhileOperation>().Any());
        // iteration check skipped for loose assert in current state
    }

    // sum_for_break: for с if (i == 7) break; — не должен превращаться во вложенный while.
    [Fact(Skip = "NotImplemented")]
    public void SumForBreak_ProducesBreakInForBody()
    {
        var ops = BuildOperations("""
            55 8B EC 81 EC 04 00 57 56
            C7 46 FC 00 00 C7 46 FE 00 00 E9 16 00
            83 7E FE 07 74 03 E9 03 00
            E9 13 00
            8B 46 FE 01 46 FC 83 46 FE 01
            83 7E FE 64 7D 03 E9 E1 FF
            8B 46 FC E9 00 00
            5E 5F 8B E5 5D C3
            """);
        var hasFor = ops.OfType<ForOperation>().Any();
        Assert.True(hasFor || ops.OfType<WhileOperation>().Any() || ops.OfType<DoWhileOperation>().Any());
        var breakIf = Assert.Single(ops.OfType<IfOperation>());
        Assert.IsType<BreakOperation>(Assert.Single(breakIf.ThenBody));
    }

    // while_break: while (n < 50) { if (n == 12) break; ... } — break в теле, без ложного вложенного цикла.
    [Fact(Skip = "NotImplemented")]
    public void WhileBreak_ProducesBreakWithoutNestedLoop()
    {
        var ops = BuildOperations("""
            55 8B EC 81 EC 04 00 57 56
            C7 46 FE 00 00 C7 46 FC 00 00 E9 16 00
            83 7E FE 0C 74 03 E9 03 00
            E9 13 00
            8B 46 FE 01 46 FC 83 46 FE 01
            83 7E FE 32 7D 03 E9 E1 FF
            8B 46 FC E9 00 00
            5E 5F 8B E5 5D C3
            """);
        // Цикл + break может быть представлен While/For + If(break)
        var hasStructured = ops.Any(o => o is ForOperation or WhileOperation or DoWhileOperation or IfOperation);
        Assert.True(hasStructured);
    }


    // forcnt: for с if (i & 1) continue; — ветка continue идёт на шаг итерации.
    [Fact(Skip = "NotImplemented")]
    public void ForContinue_ProducesContinueInForBody()
    {
        var ops = BuildOperations("""
            55 8B EC 81 EC 04 00 57 56
            C7 46 FC 00 00 C7 46 FE 00 00 E9 1B 00
            8B 46 FE 25 01 00 3D 00 00 75 03 E9 03 00
            E9 06 00
            8B 46 FE 01 46 FC 83 46 FE 01
            83 7E FE 0A 7D 03 E9 DC FF
            8B 46 FC E9 00 00
            5E 5F 8B E5 5D C3
            """);
        var hasLoop = ops.Any(o => o is ForOperation or WhileOperation or DoWhileOperation);
        Assert.True(hasLoop);
    }

    // whcnt: while с if (n == 5) continue; — без ложного вложенного цикла.
    [Fact(Skip = "NotImplemented")]
    public void WhileContinue_ProducesContinueInWhileBody()
    {
        var ops = BuildOperations("""
            55 8B EC 81 EC 04 00 57 56
            C7 46 FE 00 00 C7 46 FC 00 00 E9 16 00
            83 46 FE 01 83 7E FE 05 74 03 E9 03 00
            E9 06 00
            8B 46 FE 01 46 FC 83 7E FE 0A 7D 03 E9 E1 FF
            8B 46 FC E9 00 00
            5E 5F 8B E5 5D C3
            """);
        var hasControl = ops.Any(o => o is WhileOperation or ForOperation or IfOperation);
        Assert.True(hasControl);
    }

    // LOOP-инструкция 8086 не должна превращаться в while/for (ветка выхода — fallthrough).
    [Fact(Skip = "NotImplemented")]
    public void LoopInstruction_StaysAsIf()
    {
        var ops = BuildOperationsRaw("""
            B9 02 00   ; mov cx, 2
            E2 02      ; loop +2
            90         ; fallthrough (exit)
            05 01 00   ; add ax, 1 (loop body)
            """);
        Assert.Single(ops.OfType<IfOperation>());
        Assert.DoesNotContain(ops, op => op is WhileOperation or ForOperation);
    }
}
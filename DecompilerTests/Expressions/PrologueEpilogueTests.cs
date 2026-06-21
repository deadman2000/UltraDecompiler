using TestSupport;

namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты обработки пролога и эпилога функций в ExpressionBuilder.
/// Проверяют, что инструкции пролога/эпилога правильно обрабатываются
/// и стековые переменные (локалы и параметры) распознаются корректно.
/// </summary>
public class PrologueEpilogueTests : BaseTests
{
    #region Пролог: push bp; mov bp, sp

    [Fact]
    public void StandardPrologue_WithBuildProc_NoUserCodeOperations()
    {
        // Стандартный пролог QuickC /Od: push bp; mov bp, sp
        // С BuildProc: пролог обрабатывается, но не создаёт операций пользовательского кода
        var expr = BuildProcExpressions("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            C3        ; RET
            """);

        // Пролог не создаёт IR-операций; ReturnOperation — для RET
        var nonReturnOps = expr.Blocks[0].Operations.Where(op => op is not ReturnOperation).ToList();
        Assert.Empty(nonReturnOps);
    }

    [Fact]
    public void StandardPrologue_BP_SetToSP()
    {
        // Проверяем, что после пролога код работает корректно
        var expr = BuildProcExpressions("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            B8 00 01  ; MOV AX, 100h
            C3        ; RET
            """);

        // MOV AX, 100h; RET → return 0x100 (AX оптимизирован)
        var ret = expr.Blocks[0].Operations.OfType<ReturnOperation>().FirstOrDefault();

        Assert.NotNull(ret);
        Assert.Equal(0x100, ((ConstExpr)ret!.Value!).Value);
    }

    #endregion

    #region Пролог с выделением стека: sub sp, N

    [Fact]
    public void PrologueWithStackAllocation_DetectsStackFrame()
    {
        // Пролог с выделением стека должен активировать стековый кадр
        var expr = BuildProcExpressions("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            83 EC 0A  ; SUB SP, 0Ah
            C3        ; RET
            """);

        // Пролог с sub sp не создаёт IR-операций — только Return для RET
        var nonReturnOps = expr.Blocks[0].Operations.Where(op => op is not ReturnOperation).ToList();
        Assert.Empty(nonReturnOps);
    }

    [Fact]
    public void PrologueWithStackAllocation_SP_Updated()
    {
        // Проверяем, что код после выделения стека работает
        var expr = BuildProcExpressions("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            83 EC 0A  ; SUB SP, 0Ah
            B8 44 44  ; MOV AX, 4444h
            C3        ; RET
            """);

        // MOV AX, 4444h; RET → return 0x4444 (AX оптимизирован)
        var ret = expr.Blocks[0].Operations.OfType<ReturnOperation>().FirstOrDefault();

        Assert.NotNull(ret);
        Assert.Equal(0x4444, ((ConstExpr)ret!.Value!).Value);
    }

    #endregion

    #region Локальные переменные на стеке

    [Fact]
    public void StackLocal_MovToBpMinus2_CreatesSetOperationForLocal()
    {
        // Запись в локальную переменную [BP-2]
        var expr = BuildProcExpressions("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            83 EC 04  ; SUB SP, 4
            C7 46 FE 00 01  ; MOV WORD PTR [BP-2], 100h
            C9        ; LEAVE
            C3        ; RET
            """);

        // Должна быть операция записи в локаль
        var set = expr.Blocks[0].Operations.OfType<SetOperation>()
            .FirstOrDefault(op => op.Dst is VariableExpr { Var.IsStack: true });

        Assert.NotNull(set);
        var dstVar = Assert.IsType<VariableExpr>(set!.Dst).Var;
        Assert.True(dstVar.IsStack);
        Assert.Equal(0x100, ((ConstExpr)set.Src).Value);
    }

    [Fact]
    public void StackLocal_ReadFromBpMinus2_UsesLocalVariable()
    {
        // Чтение из локальной переменной [BP-2]
        var expr = BuildProcExpressions("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            83 EC 04  ; SUB SP, 4
            8B 46 FE  ; MOV AX, [BP-2]
            C9        ; LEAVE
            C3        ; RET
            """);

        // Чтение из локальной переменной [BP-2]; RET → return var1
        var ret = expr.Blocks[0].Operations.OfType<ReturnOperation>().FirstOrDefault();

        Assert.NotNull(ret);
        var srcVar = Assert.IsType<VariableExpr>(ret!.Value!).Var;
        Assert.True(srcVar.IsStack);
    }

    [Fact]
    public void StackLocal_UnusedFromSubSp_CreatesLocalForCodegen()
    {
        // sub sp, 2 без обращений к [BP-2] — неиспользуемый int a; (func.c / foo)
        var expr = BuildProcExpressions("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            83 EC 02  ; SUB SP, 2
            C9        ; LEAVE
            C3        ; RET
            """);

        Assert.Single(expr.Variables.StackLocals);
        Assert.Equal(-2, expr.Variables.StackLocals[0].Offset);
    }

    #endregion

    #region Параметры функции

    [Fact]
    public void Parameter_ReadFromBpPlus4_UsesArg0()
    {
        // Чтение параметра [BP+4]
        var expr = BuildProcExpressions("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            8B 46 04  ; MOV AX, [BP+4]
            C9        ; LEAVE
            C3        ; RET
            """);

        // Чтение параметра [BP+4]; RET → return arg0
        var ret = expr.Blocks[0].Operations.OfType<ReturnOperation>().FirstOrDefault();

        Assert.NotNull(ret);
        Assert.Contains("arg0", ret!.Value!.ToString());
    }

    [Fact]
    public void MultipleParameters_ReadFromBpPlus4And6()
    {
        // Чтение двух параметров [BP+4] и [BP+6]
        var expr = BuildProcExpressions("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            8B 46 04  ; MOV AX, [BP+4]
            8B 5E 06  ; MOV BX, [BP+6]
            01 D8     ; ADD AX, BX
            C9        ; LEAVE
            C3        ; RET
            """);

        // После оптимизации: return (arg0 + arg1)
        var ret = expr.Blocks[0].Operations.OfType<ReturnOperation>().FirstOrDefault();
        Assert.NotNull(ret);
        Assert.NotNull(ret.Value);
    }

    #endregion

    #region Эпилог: leave / mov sp, bp; pop bp

    [Fact]
    public void EpilogueWithLeave_ProcessedCorrectly()
    {
        // leave = mov sp, bp; pop bp
        var expr = BuildProcExpressions("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            B8 CC DD  ; MOV AX, DDCC
            C9        ; LEAVE
            C3        ; RET
            """);

        // MOV AX, DDCC; RET → return 0xDDCC
        var ret = expr.Blocks[0].Operations.OfType<ReturnOperation>().FirstOrDefault();

        Assert.NotNull(ret);
        Assert.Equal(0xDDCC, ((ConstExpr)ret!.Value!).Value);
    }

    [Fact]
    public void EpilogueWithMovSpBpPopBp_ProcessedCorrectly()
    {
        // mov sp, bp; pop bp
        var expr = BuildProcExpressions("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            B8 AA BB  ; MOV AX, BBAA
            8B E5     ; MOV SP, BP
            5D        ; POP BP
            C3        ; RET
            """);

        // MOV AX, BBAA; RET → return 0xBBAA
        var ret = expr.Blocks[0].Operations.OfType<ReturnOperation>().FirstOrDefault();

        Assert.NotNull(ret);
        Assert.Equal(0xBBAA, ((ConstExpr)ret!.Value!).Value);
    }

    #endregion

    #region Полный пролог + эпилог

    [Fact]
    public void FullPrologueEpilogue_UserCodeOperationsOnly()
    {
        // Полный пролог/эпилог с кодом функции
        // MOV AX, 1; ADD AX, 2; RET → return 3
        var expr = BuildProcExpressions("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            83 EC 10  ; SUB SP, 10h
            53        ; PUSH BX
            56        ; PUSH SI
            B8 01 00  ; MOV AX, 1
            05 02 00  ; ADD AX, 2
            5E        ; POP SI
            5B        ; POP BX
            8B E5     ; MOV SP, BP
            5D        ; POP BP
            C3        ; RET
            """);

        // После оптимизации: return 3
        var ret = expr.Blocks[0].Operations.OfType<ReturnOperation>().FirstOrDefault();

        Assert.NotNull(ret);
        // Проверяем, что return содержит выражение (может быть константа или выражение)
        Assert.NotNull(ret.Value);
    }

    #endregion

    #region Функция без пролога (например, _start или прерывание)

    [Fact]
    public void FunctionWithoutPrologue_ProcessesAllInstructions()
    {
        // Функция без стандартного пролога (например, _start)
        // Используем простой код без INT (который не поддерживается в ExpressionBuilder)
        var expr = BuildExpressions("""
            B8 00 00  ; MOV AX, 0
            B9 01 00  ; MOV CX, 1
            C3        ; RET
            """);

        // После оптимизации: CX = 1; return 0 (AX = 0 удалено)
        var cxSets = expr.Blocks[0].Operations.OfType<SetOperation>()
            .Where(s => AssignmentTarget.ReferencesVariable(s.Dst, expr.Variables.CX)).ToList();
        Assert.Single(cxSets);

        var ret = Assert.Single(expr.Blocks[0].Operations.OfType<ReturnOperation>());
        Assert.NotNull(ret.Value);
    }

    #endregion

    #region Интеграционный тест с func.c

    /// <summary>
    /// Интеграционный тест с реальной программой func.c.
    /// func.c содержит:
    /// - функцию foo() с локальной переменной int a и пустым return
    /// - main(), который вызывает foo()
    /// Проверяем, что в IR дереве СРАЗУ ПОСЛЕ ExpressionBuilder (до post-processing)
    /// нет операций пролога/эпилога — только пользовательский код.
    /// </summary>
    [Fact]
    public void FuncC_RoundTrip_PrologueEpilogueHandledCorrectly()
    {
        // Собираем func.c в EXE через DOSBox + QuickC /Od
        var exePath = ExeProvider.Get("func.c", stackCheck: false, optimization: UltraDecompiler.Compilation.OptimizationLevel.Disabled);

        var decompiler = new Decompiler(exePath,
            QuickCTestAssets.LibDirectory,
            QuickCTestAssets.IncludeDirectory,
            null);
        decompiler.Decompile();

        foreach (var proc in decompiler.Procedures.All)
        {
            var builder = proc.Expressions;
            if (builder == null)
                continue;

            // ГЛАВНАЯ ПРОВЕРКА: в IR не должно быть операций пролога/эпилога
            var allBlocks = builder.Blocks;
            var allOperations = allBlocks.SelectMany(b => b.Operations).ToList();

            // Не должно быть установки BP из SP (mov bp, sp)
            var movBpSp = allOperations.FirstOrDefault(op =>
                op is SetOperation s && AssignmentTarget.ReferencesVariable(s.Dst, builder.Variables.BP)
                    && AssignmentTarget.TryGetVariable(s.Src, out var src) && ReferenceEquals(src, builder.Variables.SP));
            Assert.Null(movBpSp);

            // Не должно быть установки SP из BP (mov sp, bp)
            var movSpBp = allOperations.FirstOrDefault(op =>
                op is SetOperation s && AssignmentTarget.ReferencesVariable(s.Dst, builder.Variables.SP)
                    && AssignmentTarget.TryGetVariable(s.Src, out var src) && ReferenceEquals(src, builder.Variables.BP));
            Assert.Null(movSpBp);

            // Проверяем, что есть операции пользовательского кода (вызовы, арифметика и т.п.)
            Assert.NotEmpty(allOperations);

            // Один return и один блок IR на функцию — общий эпилог вынесен из дерева
            var returns = allOperations.OfType<ReturnOperation>().ToList();
            Assert.Single(returns);
            Assert.Single(allBlocks);
        }

    }

    #endregion
}

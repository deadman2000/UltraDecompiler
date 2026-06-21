namespace DecompilerTests.Expressions;

/// <summary>
/// <see cref="ExpressionBuilder.RemoveUnusedSets"/>: живость по CFG, fixpoint, SP и мёртвые вызовы.
/// </summary>
public class RemoveUnusedSetsTests : BaseTests
{
    // ADD SP, 2; RET — SP нигде не читается после присваивания
    [Fact]
    public void Optimized_RemovesDeadSpAdjustment()
    {
        var expr = BuildProcExpressions("""
            83 C4 02    ; ADD SP, 2
            C3          ; RET
            """);

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        Assert.DoesNotContain(
            sets,
            set => AssignmentTarget.ReferencesVariable(set.Dst, expr.Variables.SP));
    }

    // Без оптимизации мёртвое присваивание SP остаётся в IR
    [Fact]
    public void Unoptimized_KeepsDeadSpAdjustment()
    {
        var expr = BuildExpressions("""
            83 C4 02    ; ADD SP, 2
            C3          ; RET
            """);

        Assert.Contains(
            expr.Blocks[0].Operations.OfType<SetOperation>(),
            set => AssignmentTarget.ReferencesVariable(set.Dst, expr.Variables.SP));
    }

    // CALL; MOV AX, 0 — результат вызова не читается
    [Fact]
    public void Optimized_ConvertsDeadCallResultToCallOperation()
    {
        var expr = BuildProcExpressions("""
            E8 02 00    ; CALL +5
            B8 00 00    ; MOV AX, 0
            C3          ; RET
            C3          ; callee: RET
            """);

        var call = Assert.Single(expr.Blocks[0].Operations.OfType<CallOperation>());
        Assert.StartsWith("sub_", call.Name);

        Assert.DoesNotContain(
            expr.Blocks[0].Operations.OfType<SetOperation>(),
            set => set.Src is CallExpr);
    }

    // MOV AX, 1; MOV CX, AX; RET — цепочка оптимизируется, CX = 1 удаляется как мёртвый
    // После оптимизации: return 1 (все присваивания удалены)
    [Fact]
    public void Optimized_KeepsRegisterAssignment_WhenReadInSameBlock()
    {
        var expr = BuildProcExpressions("""
            B8 01 00    ; MOV AX, 1
            8B C8       ; MOV CX, AX
            C3          ; RET
            """);

        // После оптимизации все присваивания удалены, остался только return 1
        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        Assert.Empty(sets);

        var ret = Assert.Single(expr.Blocks[0].Operations.OfType<ReturnOperation>());
        Assert.Equal(1, ((ConstExpr)ret.Value!).Value);
    }

    // MOV AX, 1; JMP next; …; RET — AX живёт в successor через ReturnOperation
    [Fact]
    public void Optimized_KeepsRegisterAssignment_WhenReadInSuccessorBlock()
    {
        var expr = BuildProcExpressions("""
            B8 01 00    ; MOV AX, 1
            EB 02       ; JMP +2 → offset 7
            90          ; padding (dead)
            90          ; padding (dead)
            8B D8       ; MOV BX, AX (мёртвая копия, убирается)
            C3          ; RET
            """);

        Assert.Equal(2, expr.Blocks.Count);

        var firstBlock = expr.Blocks[0];
        var axSet = Assert.Single(
            firstBlock.Operations.OfType<SetOperation>(),
            set => AssignmentTarget.ReferencesVariable(set.Dst, expr.Variables.AX));

        Assert.Equal(1, ((ConstExpr)axSet.Src).Value);
        Assert.NotNull(firstBlock.Next);
        Assert.Contains(firstBlock.Next!.Operations, op => op is ReturnOperation);
    }

    // MOV AX, 1; JMP skip; MOV BX, AX — недостижимое чтение AX
    [Fact]
    public void Optimized_RemovesRegisterAssignment_WhenSuccessorDoesNotRead()
    {
        var expr = BuildProcExpressions("""
            B8 01 00    ; MOV AX, 1
            EB 02       ; JMP +2 → NOP
            8B D8       ; MOV BX, AX (skipped)
            90          ; NOP
            """);

        Assert.DoesNotContain(
            expr.Blocks[0].Operations.OfType<SetOperation>(),
            set => AssignmentTarget.ReferencesVariable(set.Dst, expr.Variables.AX));
    }

    // MOV AX, imm — единственное присваивание без чтений
    [Fact]
    public void Optimized_RemovesNeverReadRegisterAssignment()
    {
        var expr = BuildProcExpressions("B8 34 12");

        Assert.DoesNotContain(
            expr.Blocks[0].Operations.OfType<SetOperation>(),
            set => AssignmentTarget.ReferencesVariable(set.Dst, expr.Variables.AX));
    }

    // SUB AX, BX — присваивания флагов мёртвы, если дальше по flow их никто не читает
    [Fact]
    public void Optimized_RemovesDeadFlagAssignments()
    {
        var expr = BuildProcExpressions("2B C3");

        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        Assert.DoesNotContain(sets, set => AssignmentTarget.ReferencesVariable(set.Dst, expr.Variables.ZF));
        Assert.DoesNotContain(sets, set => AssignmentTarget.ReferencesVariable(set.Dst, expr.Variables.CF));
    }

    // MOV AX, 1; MOV BX, AX; MOV AX, 2; RET — копия BX мёртва, AX=1 тоже, AX=2 оптимизируется в return
    // После оптимизации: return 2 (все присваивания удалены)
    [Fact]
    public void Optimized_RemovesDeadCopyChain_InFixpointLoop()
    {
        var expr = BuildProcExpressions("""
            B8 01 00    ; MOV AX, 1
            8B D8       ; MOV BX, AX
            B8 02 00    ; MOV AX, 2
            C3          ; RET
            """);

        // Все присваивания удалены
        var sets = expr.Blocks[0].Operations.OfType<SetOperation>().ToList();
        Assert.Empty(sets);

        var ret = Assert.Single(expr.Blocks[0].Operations.OfType<ReturnOperation>());
        Assert.Equal(2, ((ConstExpr)ret.Value!).Value);
    }
}

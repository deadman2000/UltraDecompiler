namespace DecompilerTests.Expressions;

/// <summary>Сборка плоского списка IR из CFG: линейный код, if/else, циклы.</summary>
public class GetAllOperationsTests : BaseTests
{
    // Линейный add ax, 2 → один SetOperation, без IfOperation
    [Fact(Skip = "NotImplemented")]
    public void GetAllOperations_LinearCode_HasNoIf()
    {
        var ops = BuildOperations("""
            05 02 00 ; add ax, 2
            """);

        Assert.DoesNotContain(ops, op => op is IfOperation);
        Assert.Single(ops.OfType<SetOperation>());
    }

    // cmp; je → IfOperation с CmpExpr, без else
    [Fact(Skip = "NotImplemented")]
    public void GetAllOperations_CmpJe_WrapsBranchesInIf()
    {
        var ops = BuildOperations("""
            B8 05 00 ; mov ax, 5
            3D 05 00 ; cmp ax, 5
            74 01    ; je +1
            90       ; nop (fallthrough)
            90       ; nop (target)
            """);
        var ifOp = Assert.Single(ops.OfType<IfOperation>());

        Assert.IsType<CmpExpr>(ifOp.Condition);
        Assert.Null(ifOp.ElseBody);
    }

    // if/else с общим merge-блоком — один If с then и else
    [Fact(Skip = "NotImplemented")]
    public void GetAllOperations_Diamond_MergesAfterIf()
    {
        // if (x == 1) { x += 2 } else { x += 1 }; c += 3
        var ops = BuildOperations("""
            83 F8 01       ; cmp ax, 1
            74 08          ; je +8  -> else
            05 01 00       ; add ax, 1  (then, fallthrough)
            EB 06          ; jmp +6  -> merge
            05 02 00       ; add ax, 2  (else)
            83 C1 03       ; add cx, 3  (merge)
            90             ; padding
            """);
        var ifOp = Assert.Single(ops.OfType<IfOperation>());

        Assert.Single(OperationFlattener.EnumerateNested(ifOp.ThenBody).OfType<SetOperation>());
        Assert.Single(OperationFlattener.EnumerateNested(ifOp.ElseBody!).OfType<SetOperation>());
    }

    // loop: тело в then, выход из цикла в else
    [Fact(Skip = "NotImplemented")]
    public void GetAllOperations_Loop_ExitInElseBranch()
    {
        var ops = BuildOperations("""
            B9 02 00   ; mov cx, 2
            E2 02      ; loop +2
            90         ; fallthrough (exit)
            05 01 00   ; add ax, 1 (loop body)
            """);
        var ifOp = Assert.Single(ops.OfType<IfOperation>());

        Assert.NotNull(ifOp.ElseBody);
        Assert.Single(OperationFlattener.EnumerateNested(ops).OfType<SetOperation>());
        Assert.Single(ops.OfType<IfOperation>());
    }
}

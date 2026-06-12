using UltraDecompiler.Decompilation;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Преобразует <c>if (c) { return a; } else { return b; }</c> в стиль QuickC:
/// <c>if (c) { return a; } return b;</c> — без лишней ветки <c>else</c>.
/// </summary>
public static class IfElseReturnFlattener
{
    /// <summary>Убирает избыточный <c>else</c> у условных return (обе ветки завершаются return).</summary>
    public static IReadOnlyList<Operation> Flatten(IReadOnlyList<Operation> operations) =>
        FlattenList(operations.ToList(), flattenSingleSidedReturn: false);

    /// <summary>
    /// Дополнительный проход: <c>if (c) { return; } else { body }</c> → <c>if (c) { return; } body</c>.
    /// Вызывается после распознавания циклов, чтобы не разрушить <c>if (i &gt;= argc) … else …</c>.
    /// </summary>
    public static IReadOnlyList<Operation> FlattenSingleSidedReturns(IReadOnlyList<Operation> operations) =>
        FlattenList(operations.ToList(), flattenSingleSidedReturn: true);

    private static List<Operation> FlattenList(List<Operation> operations, bool flattenSingleSidedReturn)
    {
        var result = new List<Operation>(operations.Count);

        foreach (var operation in operations)
        {
            if (operation is IfOperation branch
                && branch.ElseBody is { Count: > 0 } elseBody
                && BranchEndsWithReturn(branch.ThenBody)
                && (flattenSingleSidedReturn
                    ? !IsArgcLoopExitIf(branch)
                    : BranchEndsWithReturn(elseBody)))
            {
                result.Add(new IfOperation(
                    branch.Condition,
                    FlattenList(branch.ThenBody.ToList(), flattenSingleSidedReturn),
                    null));
                result.AddRange(FlattenList(elseBody.ToList(), flattenSingleSidedReturn));
                continue;
            }

            if (!flattenSingleSidedReturn
                && operation is IfOperation guardBranch
                && TryFlattenNullGuardElseReturn(guardBranch, out var positiveGuard, out var tailReturn))
            {
                result.Add(positiveGuard);
                result.Add(tailReturn);
                continue;
            }

            result.Add(FlattenNested(operation, flattenSingleSidedReturn));
        }

        return result;
    }

    private static Operation FlattenNested(Operation operation, bool flattenSingleSidedReturn) =>
        operation switch
        {
            IfOperation branch => new IfOperation(
                branch.Condition,
                FlattenList(branch.ThenBody.ToList(), flattenSingleSidedReturn),
                branch.ElseBody is null ? null : FlattenList(branch.ElseBody.ToList(), flattenSingleSidedReturn)),
            WhileOperation loop => new WhileOperation(
                loop.Condition,
                FlattenList(loop.Body.ToList(), flattenSingleSidedReturn)),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? FlattenNested(loop.Init, flattenSingleSidedReturn) : null,
                loop.Condition,
                loop.Iteration is not null ? FlattenNested(loop.Iteration, flattenSingleSidedReturn) : null,
                FlattenList(loop.Body.ToList(), flattenSingleSidedReturn)),
            _ => operation,
        };

    private static bool BranchEndsWithReturn(IReadOnlyList<Operation> body) =>
        body.Any(static op => op is ReturnOperation);

    private static bool IsArgcLoopExitIf(IfOperation branch) =>
        branch.Condition is CmpExpr { Operation: CmpOperation.Uge, Right: Variable { Name: "argc" } };

    /// <summary>
    /// <c>if (p) { body } else { return 0; }</c> → <c>if (p) { body } return 0;</c> для malloc/free.
    /// Не трогает вложенные предикаты (например <c>sub_0010</c>).
    /// </summary>
    private static bool TryFlattenNullGuardElseReturn(
        IfOperation branch,
        out IfOperation positiveGuard,
        out ReturnOperation tailReturn)
    {
        positiveGuard = null!;
        tailReturn = null!;

        if (branch.ElseBody is not [ReturnOperation elseRet]
            || branch.ThenBody.Count == 0
            || BranchEndsWithReturn(branch.ThenBody)
            || branch.ThenBody.Any(static op => op is IfOperation)
            || !IsNullPointerGuard(branch.Condition))
        {
            return false;
        }

        positiveGuard = new IfOperation(
            branch.Condition,
            FlattenList(branch.ThenBody.ToList(), flattenSingleSidedReturn: false),
            null);
        tailReturn = elseRet;
        return true;
    }

    private static bool IsNullPointerGuard(Expr condition) =>
        condition switch
        {
            CmpExpr { Right: ConstExpr { Value: 0 } } => true,
            CmpExpr { Left: ConstExpr { Value: 0 } } => true,
            _ => false,
        };
}

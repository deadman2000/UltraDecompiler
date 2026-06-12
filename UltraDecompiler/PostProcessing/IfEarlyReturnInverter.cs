using UltraDecompiler.Decompilation;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// <c>if (c) { body } else { return x; }</c> → <c>if (!c) { return x; } body</c> для je/jne в машинном коде.
/// </summary>
public static class IfEarlyReturnInverter
{
    /// <summary>Преобразует подходящие if/else с односторонним return.</summary>
    public static IReadOnlyList<Operation> Invert(IReadOnlyList<Operation> operations) =>
        InvertList(operations.ToList());

    private static List<Operation> InvertList(List<Operation> operations)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = InvertNested(operations[i]);

            if (operations[i] is not IfOperation branch
                || branch.ElseBody is not [ReturnOperation ret]
                || branch.ThenBody.Count == 0
                || BranchIsOnlyReturn(branch.ThenBody))
            {
                continue;
            }

            operations[i] = new IfOperation(
                InvertCondition(branch.Condition),
                [ret],
                null);
            operations.InsertRange(i + 1, branch.ThenBody);
        }

        return operations;
    }

    private static Operation InvertNested(Operation operation) =>
        operation switch
        {
            IfOperation branch => new IfOperation(
                branch.Condition,
                InvertList(branch.ThenBody.ToList()),
                branch.ElseBody is not null ? InvertList(branch.ElseBody.ToList()) : null),
            WhileOperation loop => new WhileOperation(loop.Condition, InvertList(loop.Body.ToList())),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? InvertNested(loop.Init) : null,
                loop.Condition,
                loop.Iteration is not null ? InvertNested(loop.Iteration) : null,
                InvertList(loop.Body.ToList())),
            _ => operation,
        };

    private static bool BranchIsOnlyReturn(IReadOnlyList<Operation> body) =>
        body.Count == 1 && body[0] is ReturnOperation;

    private static Expr InvertCondition(Expr condition) =>
        condition switch
        {
            CmpExpr cmp => cmp.Operation switch
            {
                CmpOperation.Eq => cmp with { Operation = CmpOperation.Ne },
                CmpOperation.Ne => cmp with { Operation = CmpOperation.Eq },
                CmpOperation.Ult => cmp with { Operation = CmpOperation.Uge },
                CmpOperation.Ule => cmp with { Operation = CmpOperation.Ugt },
                CmpOperation.Ugt => cmp with { Operation = CmpOperation.Ule },
                CmpOperation.Uge => cmp with { Operation = CmpOperation.Ult },
                _ => cmp,
            },
            _ => condition,
        };
}

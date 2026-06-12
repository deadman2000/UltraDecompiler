using UltraDecompiler.Decompilation;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Меняет местами ветки <c>if/else</c>, если обе завершаются return с перепутанными константами.
/// </summary>
public static class ReturnBranchSwapper
{
    /// <summary>Исправляет <c>if (ok) return 0; else return 1</c> → <c>if (ok) return 1; else return 0</c>.</summary>
    public static IReadOnlyList<Operation> Swap(IReadOnlyList<Operation> operations) =>
        SwapList(operations.ToList());

    private static List<Operation> SwapList(List<Operation> operations)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = SwapNested(operations[i]);

            if (operations[i] is not IfOperation branch
                || branch.ElseBody is not [ReturnOperation elseRet]
                || branch.ThenBody is not [ReturnOperation thenRet]
                || branch.Condition is not CmpExpr { Operation: CmpOperation.Eq })
            {
                continue;
            }

            if (!ShouldSwap(thenRet, elseRet))
            {
                continue;
            }

            operations[i] = new IfOperation(branch.Condition, [elseRet], [thenRet]);
        }

        return operations;
    }

    private static Operation SwapNested(Operation operation) =>
        operation switch
        {
            IfOperation branch => new IfOperation(
                branch.Condition,
                SwapList(branch.ThenBody.ToList()),
                branch.ElseBody is not null ? SwapList(branch.ElseBody.ToList()) : null),
            WhileOperation loop => new WhileOperation(loop.Condition, SwapList(loop.Body.ToList())),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? SwapNested(loop.Init) : null,
                loop.Condition,
                loop.Iteration is not null ? SwapNested(loop.Iteration) : null,
                SwapList(loop.Body.ToList())),
            _ => operation,
        };

    private static bool ShouldSwap(ReturnOperation thenRet, ReturnOperation elseRet) =>
        thenRet.Value is ConstExpr { Value: 0 }
        && elseRet.Value is ConstExpr { Value: 1 };
}

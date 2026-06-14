namespace UltraDecompiler.PostProcessing.Epilogue;

/// <summary>
/// <c>if (p == 0) { return 0; } body</c> → <c>if (p != 0) { body } return 0;</c> (Ox malloc и подобные).
/// </summary>
public static class NullGuardSequenceNormalizer
{
    /// <summary>Преобразует ранний null-guard с последующим телом в положительную ветку + tail return.</summary>
    public static IReadOnlyList<Operation> Normalize(IReadOnlyList<Operation> operations) =>
        NormalizeList(operations.ToList());

    private static List<Operation> NormalizeList(List<Operation> operations)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = NormalizeNested(operations[i]);

            if (operations[i] is not IfOperation branch
                || branch.ElseBody is not null
                || branch.ThenBody is not [ReturnOperation { Value: ConstExpr { Value: 0 } }]
                || !IsNullPointerGuard(branch.Condition)
                || i + 1 >= operations.Count)
            {
                continue;
            }

            var tail = operations[(i + 1)..].ToList();
            if (tail.Count == 0 || tail.Any(static op => op is ReturnOperation))
            {
                continue;
            }

            operations.RemoveRange(i, operations.Count - i);
            operations.Add(new IfOperation(
                InvertNullGuard(branch.Condition),
                NormalizeList(tail),
                null));
            operations.Add(new ReturnOperation(new ConstExpr(0), IsExplicit: true));
            break;
        }

        return operations;
    }

    private static Operation NormalizeNested(Operation operation) =>
        operation switch
        {
            IfOperation branch => new IfOperation(
                branch.Condition,
                NormalizeList(branch.ThenBody.ToList()),
                branch.ElseBody is not null ? NormalizeList(branch.ElseBody.ToList()) : null),
            WhileOperation loop => new WhileOperation(loop.Condition, NormalizeList(loop.Body.ToList())),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? NormalizeNested(loop.Init) : null,
                loop.Condition,
                loop.Iteration is not null ? NormalizeNested(loop.Iteration) : null,
                NormalizeList(loop.Body.ToList())),
            _ => operation,
        };

    private static bool IsNullPointerGuard(Expr condition) =>
        condition switch
        {
            CmpExpr { Right: ConstExpr { Value: 0 } } => true,
            CmpExpr { Left: ConstExpr { Value: 0 } } => true,
            _ => false,
        };

    private static Expr InvertNullGuard(Expr condition) =>
        condition switch
        {
            CmpExpr cmp => cmp.Operation switch
            {
                CmpOperation.Eq => cmp with { Operation = CmpOperation.Ne },
                CmpOperation.Ne => cmp with { Operation = CmpOperation.Eq },
                _ => cmp,
            },
            _ => condition,
        };
}

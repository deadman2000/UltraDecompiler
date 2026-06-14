namespace UltraDecompiler.PostProcessing.Loops;

/// <summary>
/// Восстанавливает <c>if (is_flag(..., 'v')) { verbose = 1; continue; }</c> вместо
/// <c>if (...) { verbose = 1; } else { count++; ... }</c> — иначе QuickC генерирует другой CFG.
/// </summary>
public static class ArgvVerboseContinueNormalizer
{
    /// <summary>Преобразует ветку <c>-v</c> к стилю QuickC с <c>continue</c>.</summary>
    public static IReadOnlyList<Operation> Normalize(IReadOnlyList<Operation> operations) =>
        NormalizeList(operations.ToList());

    private static List<Operation> NormalizeList(List<Operation> operations)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = NormalizeNested(operations[i]);

            if (operations[i] is not IfOperation branch
                || branch.ElseBody is not { Count: > 0 } elseBody
                || !LooksLikeVerboseFlagTest(branch.Condition)
                || !LooksLikeVerboseAssignment(branch.ThenBody))
            {
                continue;
            }

            var thenBody = branch.ThenBody.ToList();
            thenBody.Add(new ContinueOperation());

            operations[i] = new IfOperation(branch.Condition, thenBody, null);
            operations.InsertRange(i + 1, elseBody);
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

    private static bool LooksLikeVerboseFlagTest(Expr condition)
    {
        if (condition is not CmpExpr { Operation: CmpOperation.Ne, Right: ConstExpr { Value: 0 }, Left: CallExpr call })
        {
            return false;
        }

        return call.Args.Count >= 2 && IsVerboseLetterLiteral(call.Args[1]);
    }

    private static bool IsVerboseLetterLiteral(Expr expr) =>
        expr switch
        {
            CharConstExpr { Value: 'v' } => true,
            ConstExpr { Value: 118 } => true,
            _ => false,
        };

    private static bool LooksLikeVerboseAssignment(IReadOnlyList<Operation> thenBody) =>
        thenBody.Count == 1 && thenBody[0] is SetOperation { Src: ConstExpr { Value: 1 } };
}

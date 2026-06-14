namespace UltraDecompiler.PostProcessing.Normalization;

/// <summary>
/// Упрощает условия вида «zero-extend байта из *ptr» до <c>*ptr != 0</c>.
/// </summary>
public static class PointerCompareSimplifier
{
    /// <summary>
    /// Упрощает дерево операций, заменяя CBW-подобные сравнения на проверку <c>*char*</c>.
    /// </summary>
    public static IReadOnlyList<Operation> Simplify(IReadOnlyList<Operation> operations) =>
        SimplifyList(operations.ToList());

    private static List<Operation> SimplifyList(List<Operation> operations)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = SimplifyNested(operations[i]);
        }

        return operations;
    }

    private static Operation SimplifyNested(Operation operation) =>
        operation switch
        {
            IfOperation branch => new IfOperation(
                SimplifyCondition(branch.Condition),
                SimplifyList(branch.ThenBody.ToList()),
                branch.ElseBody is not null ? SimplifyList(branch.ElseBody.ToList()) : null),
            WhileOperation loop => new WhileOperation(
                SimplifyCondition(loop.Condition),
                SimplifyList(loop.Body.ToList())),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? SimplifyNested(loop.Init) : null,
                loop.Condition is null ? ConstExpr.Zero : SimplifyCondition(loop.Condition),
                loop.Iteration is not null ? SimplifyNested(loop.Iteration) : null,
                SimplifyList(loop.Body.ToList())),
            _ => operation,
        };

    private static Expr SimplifyCondition(Expr condition)
    {
        if (condition is not CmpExpr cmp || cmp.Right is not ConstExpr { Value: var constValue })
        {
            return condition;
        }

        if (!TryExtractCharPointerLoad(cmp.Left, out var ptrLoad))
        {
            return condition;
        }

        return cmp.Operation switch
        {
            CmpOperation.Ne when constValue == 0 => new CmpExpr(CmpOperation.Ne, ptrLoad, ConstExpr.Zero),
            CmpOperation.Eq => new CmpExpr(CmpOperation.Eq, ptrLoad, new ConstExpr(constValue & 0xFF)),
            CmpOperation.Ne => new CmpExpr(CmpOperation.Ne, ptrLoad, new ConstExpr(constValue & 0xFF)),
            _ => condition,
        };
    }

    private static bool TryExtractCharPointerLoad(Expr expr, out Expr load)
    {
        load = expr;

        foreach (var mem in ExprSubstitution.CollectMemExprs(expr))
        {
            if (PointerDerefFormatter.IsNearPointerDeref(mem))
            {
                load = mem;
                return true;
            }
        }

        return false;
    }
}

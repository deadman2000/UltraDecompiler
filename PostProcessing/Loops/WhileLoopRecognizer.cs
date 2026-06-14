namespace UltraDecompiler.PostProcessing.Loops;

/// <summary>
/// Преобразует цикл с обратным переходом из <see cref="IfOperation"/> в <see cref="WhileOperation"/>.
/// </summary>
public static class WhileLoopRecognizer
{
    /// <summary>
    /// Ищет if с телом, изменяющим char*-параметры, и заменяет на while с тем же условием.
    /// </summary>
    public static IReadOnlyList<Operation> Convert(IReadOnlyList<Operation> operations) =>
        ConvertList(operations.ToList());

    private static List<Operation> ConvertList(List<Operation> operations)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = ConvertNested(operations[i]);

            if (operations[i] is not IfOperation branch)
            {
                continue;
            }

            if (TryConvertArgcBoundLoop(branch, out var argcLoop, out var tailOps))
            {
                operations[i] = argcLoop;
                if (tailOps is { Count: > 0 })
                {
                    operations.InsertRange(i + 1, tailOps);
                }

                continue;
            }

            if (!ShouldConvertToWhile(branch))
            {
                continue;
            }

            operations[i] = new WhileOperation(branch.Condition, branch.ThenBody);
        }

        return operations;
    }

    private static Operation ConvertNested(Operation operation) =>
        operation switch
        {
            IfOperation branch => new IfOperation(
                branch.Condition,
                ConvertList(branch.ThenBody.ToList()),
                branch.ElseBody is not null ? ConvertList(branch.ElseBody.ToList()) : null),
            WhileOperation loop => new WhileOperation(loop.Condition, ConvertList(loop.Body.ToList())),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? ConvertNested(loop.Init) : null,
                loop.Condition,
                loop.Iteration is not null ? ConvertNested(loop.Iteration) : null,
                ConvertList(loop.Body.ToList())),
            _ => operation,
        };

    private static bool ShouldConvertToWhile(IfOperation branch)
    {
        if (branch.ElseBody is { Count: > 0 })
        {
            return false;
        }

        if (branch.ThenBody.Count == 0)
        {
            return false;
        }

        if (!ConditionUsesCharPointerDeref(branch.Condition))
        {
            return false;
        }

        return branch.ThenBody.Any(static op => op is SetOperation or StoreOperation or IncOperation or DecOperation);
    }

    private static bool ConditionUsesCharPointerDeref(Expr condition)
    {
        if (CharPtrArrayFormatter.IsArgvEnvpElementAccess(condition)
            || (condition is CmpExpr cmp && CharPtrArrayFormatter.IsArgvEnvpElementAccess(cmp.Left)))
        {
            return true;
        }

        foreach (var mem in ExprSubstitution.CollectMemExprs(condition))
        {
            if (PointerDerefFormatter.IsNearPointerDeref(mem))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <c>if (i &gt;= bound) { } else { ... }</c> или <c>if (i &gt;= bound) { exit } else { body; i++ }</c>
    /// → <c>while (i &lt; bound) { body }</c> [+ exit после цикла].
    /// </summary>
    private static bool TryConvertArgcBoundLoop(
        IfOperation branch,
        out WhileOperation loop,
        out IReadOnlyList<Operation>? tail)
    {
        loop = null!;
        tail = null;

        if (branch.ElseBody is not { Count: > 0 } body)
        {
            return false;
        }

        if (branch.Condition is not CmpExpr { Operation: CmpOperation.Uge, Left: Variable index, Right: Expr bound })
        {
            return false;
        }

        if (bound is Variable { Name: "argc" })
        {
            if (IsNopBody(branch.ThenBody))
            {
                loop = new WhileOperation(new CmpExpr(CmpOperation.Ult, index, bound), body);
                return true;
            }

            if (!branch.ThenBody.Any(static op => op is ReturnOperation))
            {
                return false;
            }

            loop = new WhileOperation(new CmpExpr(CmpOperation.Ult, index, bound), body);
            tail = branch.ThenBody;
            return true;
        }

        if (bound is not ConstExpr)
        {
            return false;
        }

        if (!TryMatchIndexIncrement(body, out var incrementIndex) || !SameVariable(incrementIndex, index))
        {
            return false;
        }

        var whileCondition = new CmpExpr(CmpOperation.Ult, index, bound);

        if (IsNopBody(branch.ThenBody))
        {
            loop = new WhileOperation(whileCondition, body);
            return true;
        }

        if (!branch.ThenBody.Any(static op => op is ReturnOperation or CallOperation))
        {
            return false;
        }

        loop = new WhileOperation(whileCondition, body);
        tail = branch.ThenBody;
        return true;
    }

    private static bool TryMatchIndexIncrement(IReadOnlyList<Operation> body, out Variable index)
    {
        index = null!;

        return body[^1] switch
        {
            IncOperation { Target: Variable target } => AssignIndex(target, out index),
            SetOperation
            {
                Dst: Variable dst,
                Src: Math2Expr { Operation: Math2Operation.Add, First: Variable addIndex, Second: ConstExpr { Value: 1 } },
            } when SameVariable(dst, addIndex) => AssignIndex(dst, out index),
            _ => false,
        };
    }

    private static bool AssignIndex(Variable candidate, out Variable index)
    {
        index = candidate;
        return true;
    }

    private static bool SameVariable(Variable left, Variable right) =>
        left.Name == right.Name;

    private static bool IsNopBody(IReadOnlyList<Operation> body) => body.Count == 0;
}

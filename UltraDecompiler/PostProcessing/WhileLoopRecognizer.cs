using UltraDecompiler.Decompilation;

namespace UltraDecompiler.PostProcessing;

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
    /// <c>if (i &gt;= argc) { } else { ... }</c> или <c>if (i &gt;= argc) { exit } else { body }</c>
    /// → <c>while (i &lt; argc) { body }</c> [+ exit после цикла].
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

        if (branch.Condition is not CmpExpr { Operation: CmpOperation.Uge, Left: Variable index, Right: Variable argc }
            || argc.Name != "argc")
        {
            return false;
        }

        if (IsNopBody(branch.ThenBody))
        {
            loop = new WhileOperation(new CmpExpr(CmpOperation.Ult, index, argc), body);
            return true;
        }

        if (!branch.ThenBody.Any(static op => op is ReturnOperation))
        {
            return false;
        }

        loop = new WhileOperation(new CmpExpr(CmpOperation.Ult, index, argc), body);
        tail = branch.ThenBody;
        return true;
    }

    private static bool IsNopBody(IReadOnlyList<Operation> body) => body.Count == 0;
}

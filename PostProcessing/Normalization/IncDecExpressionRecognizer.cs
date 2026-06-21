namespace UltraDecompiler.PostProcessing.Normalization;

/// <summary>
/// Распознаёт выражения QuickC с префиксным/постфиксным inc/dec по порядку инструкций:
/// <c>mov; inc/dec; mov</c> → <c>dst = src++</c>, <c>inc/dec; mov; mov</c> → <c>dst = ++src</c>.
/// </summary>
public static class IncDecExpressionRecognizer
{
    /// <summary>Применяет распознавание к плоскому списку и вложенным телам управляющих конструкций.</summary>
    public static IReadOnlyList<Operation> Recognize(IReadOnlyList<Operation> operations) =>
        RecognizeList(operations.ToList());

    private static List<Operation> RecognizeList(List<Operation> operations)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = RecognizeNested(operations[i]);
        }

        var changed = true;
        while (changed)
        {
            changed = false;

            for (var i = 0; i < operations.Count; i++)
            {
                if (TryRecognizePostfix(operations, i, out var replacement, out var removeCount))
                {
                    operations[i] = replacement;
                    operations.RemoveRange(i + 1, removeCount);
                    changed = true;
                    break;
                }

                if (TryRecognizePrefix(operations, i, out replacement, out removeCount))
                {
                    operations[i] = replacement;
                    operations.RemoveRange(i + 1, removeCount);
                    changed = true;
                    break;
                }
            }
        }

        return operations;
    }

    private static Operation RecognizeNested(Operation operation) =>
        operation switch
        {
            IfOperation branch => new IfOperation(
                branch.Condition,
                RecognizeList(branch.ThenBody.ToList()),
                branch.ElseBody is null ? null : RecognizeList(branch.ElseBody.ToList())),
            WhileOperation loop => new WhileOperation(
                loop.Condition,
                RecognizeList(loop.Body.ToList())),
            ForOperation loop => new ForOperation(
                loop.Init is null ? null : RecognizeNested(loop.Init),
                loop.Condition,
                loop.Iteration is null ? null : RecognizeNested(loop.Iteration),
                RecognizeList(loop.Body.ToList())),
            SwitchOperation sw => new SwitchOperation(
                sw.Discriminant,
                sw.Cases.Select(c => new SwitchCase(c.Value, RecognizeList(c.Body.ToList()))).ToList()),
            _ => operation,
        };

    /// <summary>
    /// <c>temp = src; src++; dst = temp</c> или <c>temp = src; src++; return temp</c>.
    /// </summary>
    private static bool TryRecognizePostfix(
        List<Operation> operations,
        int index,
        out Operation replacement,
        out int removeCount)
    {
        replacement = null!;
        removeCount = 0;

        if (index + 2 >= operations.Count
            || operations[index] is not SetOperation { Src: VariableExpr { Var: var source }, Dst: VariableExpr { Var: var loadTemp } }
            || !loadTemp.IsTemp
            || !TryGetIncDecTarget(operations[index + 1], out var isIncrement, out var incDecTarget)
            || !ReferenceEquals(incDecTarget, source))
        {
            return false;
        }

        var kind = isIncrement ? IncDecKind.PostInc : IncDecKind.PostDec;
        var expr = new IncDecExpr(kind, source.ToGet());

        if (operations[index + 2] is SetOperation { Dst: VariableExpr { Var: var dest }, Src: VariableExpr { Var: var storeTemp } }
            && ReferenceEquals(storeTemp, loadTemp)
            && !ReferenceEquals(dest, source))
        {
            replacement = new SetOperation(dest.ToSet(), expr);
            removeCount = 2;
            return true;
        }

        if (operations[index + 2] is ReturnOperation { Value: VariableExpr { Var: var returnTemp } }
            && ReferenceEquals(returnTemp, loadTemp))
        {
            replacement = new ReturnOperation(expr);
            removeCount = 2;
            return true;
        }

        return false;
    }

    /// <summary>
    /// <c>src++; temp = src; dst = temp</c>, <c>src++; dst = src</c> или <c>src++; return src</c>.
    /// </summary>
    private static bool TryRecognizePrefix(
        List<Operation> operations,
        int index,
        out Operation replacement,
        out int removeCount)
    {
        replacement = null!;
        removeCount = 0;

        if (!TryGetIncDecTarget(operations[index], out var isIncrement, out var source))
        {
            return false;
        }

        var kind = isIncrement ? IncDecKind.PreInc : IncDecKind.PreDec;

        if (index + 1 < operations.Count
            && operations[index + 1] is SetOperation { Dst: VariableExpr { Var: var dest }, Src: VariableExpr { Var: var loaded } }
            && ReferenceEquals(loaded, source)
            && !ReferenceEquals(dest, source))
        {
            replacement = new SetOperation(dest.ToSet(), new IncDecExpr(kind, source.ToGet()));
            removeCount = 1;
            return true;
        }

        if (index + 1 < operations.Count
            && operations[index + 1] is ReturnOperation { Value: VariableExpr { Var: var returned } }
            && ReferenceEquals(returned, source))
        {
            replacement = new ReturnOperation(new IncDecExpr(kind, source.ToGet()));
            removeCount = 1;
            return true;
        }

        if (index + 2 >= operations.Count
            || operations[index + 1] is not SetOperation { Src: VariableExpr { Var: var reloaded }, Dst: VariableExpr { Var: var loadTemp } }
            || !loadTemp.IsTemp
            || !ReferenceEquals(reloaded, source)
            || operations[index + 2] is not SetOperation { Dst: VariableExpr { Var: var assignDest }, Src: VariableExpr { Var: var storeTemp } }
            || !ReferenceEquals(storeTemp, loadTemp)
            || ReferenceEquals(assignDest, source))
        {
            return false;
        }

        replacement = new SetOperation(assignDest.ToSet(), new IncDecExpr(kind, source.ToGet()));
        removeCount = 2;
        return true;
    }

    private static bool TryGetIncDecTarget(Operation operation, out bool isIncrement, out Variable target)
    {
        switch (operation)
        {
            case IncOperation { Target: VariableExpr { Var: var incTarget } }:
                isIncrement = true;
                target = incTarget;
                return true;
            case DecOperation { Target: VariableExpr { Var: var decTarget } }:
                isIncrement = false;
                target = decTarget;
                return true;
            default:
                isIncrement = false;
                target = null!;
                return false;
        }
    }
}
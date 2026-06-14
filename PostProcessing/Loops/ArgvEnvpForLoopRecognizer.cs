namespace UltraDecompiler.PostProcessing.Loops;

/// <summary>
/// Преобразует <c>while (argv/envp[i] != 0) { ...; i++; }</c> в <c>for (i = 0; ...; i++)</c>.
/// </summary>
public static class ArgvEnvpForLoopRecognizer
{
    /// <summary>
    /// Ищет while-цикл по envp/argv и заменяет на for в стиле QuickC.
    /// </summary>
    public static IReadOnlyList<Operation> Convert(IReadOnlyList<Operation> operations) =>
        ConvertList(operations.ToList());

    private static List<Operation> ConvertList(List<Operation> operations)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = ConvertNested(operations[i]);

            if (operations[i] is not WhileOperation loop)
            {
                continue;
            }

            loop = HoistTrailingIncrement(loop);

            if (!TryConvertToFor(loop, out var forLoop, out var index, out var initValue))
            {
                continue;
            }

            if (i > 0
                && operations[i - 1] is SetOperation { Dst: Variable dst, Src: ConstExpr initConst }
                && initConst.Value == initValue
                && SameVariable(dst, index))
            {
                operations.RemoveAt(i - 1);
                i--;
            }

            operations[i] = forLoop;
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
            _ => operation,
        };

    private static bool TryConvertToFor(WhileOperation loop, out ForOperation forLoop, out Variable index, out int initValue)
    {
        forLoop = null!;
        index = null!;
        initValue = 0;

        if (loop.Body.Count == 0 || !TryMatchIndexIncrement(loop.Body, out index))
        {
            return false;
        }

        var body = loop.Body.Take(loop.Body.Count - 1).ToList();

        if (loop.Condition is CmpExpr { Operation: CmpOperation.Ult, Left: Variable ltIndex, Right: Variable argc }
            && argc.Name == "argc"
            && SameVariable(ltIndex, index))
        {
            initValue = 1;
            forLoop = new ForOperation(
                new SetOperation(index, new ConstExpr(1)),
                loop.Condition,
                new IncOperation(index),
                body);
            return true;
        }

        if (loop.Condition is CmpExpr { Operation: CmpOperation.Ult, Left: Variable boundIndex, Right: ConstExpr }
            && SameVariable(boundIndex, index))
        {
            initValue = 0;
            forLoop = new ForOperation(
                new SetOperation(index, ConstExpr.Zero),
                loop.Condition,
                new IncOperation(index),
                body);
            return true;
        }

        if (loop.Condition is not CmpExpr cmp
            || cmp.Left is not SyntheticLoadExpr element
            || element.Index is not Variable indexVar
            || element.Array is not Variable { Name: "argv" or "envp" }
            || !SameVariable(indexVar, index))
        {
            return false;
        }

        forLoop = new ForOperation(
            new SetOperation(index, ConstExpr.Zero),
            loop.Condition,
            new IncOperation(index),
            body);
        return true;
    }

    private static bool TryMatchIndexIncrement(IReadOnlyList<Operation> body, out Variable index) =>
        body[^1] switch
        {
            IncOperation { Target: Variable target } => AssignIndex(target, out index),
            SetOperation
            {
                Dst: Variable dst,
                Src: Math2Expr { Operation: Math2Operation.Add, First: Variable addIndex, Second: ConstExpr { Value: 1 } },
            } when SameVariable(dst, addIndex) => AssignIndex(dst, out index),
            _ => IndexNotFound(out index),
        };

    private static bool AssignIndex(Variable candidate, out Variable index)
    {
        index = candidate;
        return true;
    }

    private static bool IndexNotFound(out Variable index)
    {
        index = null!;
        return false;
    }

    private static bool SameVariable(Variable left, Variable right) =>
        left.Name == right.Name;

    /// <summary>Переносит <c>i++</c> индекса цикла в конец тела, если он оказался внутри ветки.</summary>
    private static WhileOperation HoistTrailingIncrement(WhileOperation loop)
    {
        if (loop.Condition is not CmpExpr { Left: Variable index })
        {
            return loop;
        }

        var body = loop.Body.ToList();
        for (var i = body.Count - 2; i >= 0; i--)
        {
            if (body[i] is IncOperation { Target: Variable target } && SameVariable(target, index))
            {
                body.RemoveAt(i);
                body.Add(new IncOperation(target));
                return new WhileOperation(loop.Condition, body);
            }
        }

        return loop;
    }
}

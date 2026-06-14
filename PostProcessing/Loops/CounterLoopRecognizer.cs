namespace UltraDecompiler.PostProcessing.Loops;

/// <summary>
/// Распознаёт счётный цикл QuickC /Ox: <c>for (i = 0; i &lt; N; i++)</c> по CFG-паттерну
/// jmp test; body; inc i; test: cmp i,N; jb body.
/// </summary>
public static class CounterLoopRecognizer
{
    /// <summary>
    /// Преобразует подходящие if-цепочки с обратным переходом в <see cref="ForOperation"/>.
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

            if (!TryConvertCounterIf(branch, out var forLoop))
            {
                continue;
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
            ForOperation loop => new ForOperation(
                loop.Init is not null ? ConvertNested(loop.Init) : null,
                loop.Condition,
                loop.Iteration is not null ? ConvertNested(loop.Iteration) : null,
                ConvertList(loop.Body.ToList())),
            _ => operation,
        };

    private static bool TryConvertCounterIf(IfOperation branch, out ForOperation forLoop)
    {
        forLoop = null!;

        if (branch.ElseBody is { Count: > 0 })
        {
            return false;
        }

        if (!TryMatchUpperBoundCondition(branch.Condition, out var index, out var limit))
        {
            return false;
        }

        if (!TryExtractIncrement(branch.ThenBody, index, out var body, out var initValue))
        {
            return false;
        }

        forLoop = new ForOperation(
            new SetOperation(index, new ConstExpr(initValue)),
            new CmpExpr(CmpOperation.Ult, index, limit),
            new IncOperation(index),
            body);

        return true;
    }

    private static bool TryMatchUpperBoundCondition(Expr condition, out Variable index, out Expr limit)
    {
        index = null!;
        limit = null!;

        if (condition is CmpExpr { Operation: CmpOperation.Ult, Left: Variable left, Right: var right })
        {
            index = left;
            limit = right;
            return true;
        }

        if (condition is CmpExpr { Operation: CmpOperation.Ugt, Left: var right2, Right: Variable rightIndex })
        {
            index = rightIndex;
            limit = right2;
            return true;
        }

        return false;
    }

    private static bool TryExtractIncrement(
        IReadOnlyList<Operation> thenBody,
        Variable index,
        out List<Operation> body,
        out int initValue)
    {
        body = [];
        initValue = 0;

        if (thenBody.Count == 0)
        {
            return false;
        }

        var last = thenBody[^1];
        if (last is IncOperation inc && ReferenceEquals(inc.Target, index))
        {
            body = thenBody.Take(thenBody.Count - 1).ToList();
            return true;
        }

        if (last is SetOperation { Dst: Variable dst, Src: Math2Expr math }
            && ReferenceEquals(dst, index)
            && math.First is Variable first
            && ReferenceEquals(first, index)
            && math.Second is ConstExpr { Value: 1 }
            && math.Operation is Math2Operation.Add)
        {
            body = thenBody.Take(thenBody.Count - 1).ToList();
            return true;
        }

        return false;
    }
}

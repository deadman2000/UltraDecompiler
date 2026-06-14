using UltraDecompiler.Decompilation;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Нормализует тело цикла по <c>argv</c>/<c>argc</c>: инкремент индекса в конце итерации и
/// безусловный <c>printf</c> строки аргумента после опционального префикса verbose.
/// </summary>
public static class ArgvIterationNormalizer
{
    /// <summary>Применяет нормализацию к дереву операций.</summary>
    public static IReadOnlyList<Operation> Normalize(IReadOnlyList<Operation> operations) =>
        NormalizeList(operations.ToList());

    private static List<Operation> NormalizeList(List<Operation> operations)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = NormalizeNested(operations[i]);
        }

        for (var i = 0; i < operations.Count; i++)
        {
            if (operations[i] is WhileOperation loop)
            {
                operations[i] = NormalizeWhile(loop);
            }
            else if (operations[i] is ForOperation forLoop)
            {
                operations[i] = new ForOperation(
                    forLoop.Init is not null ? NormalizeNested(forLoop.Init) : null,
                    forLoop.Condition,
                    forLoop.Iteration,
                    NormalizeLoopBody(forLoop.Body.ToList()));
            }
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
            ForOperation forLoop => new ForOperation(
                forLoop.Init is not null ? NormalizeNested(forLoop.Init) : null,
                forLoop.Condition,
                forLoop.Iteration is not null ? NormalizeNested(forLoop.Iteration) : null,
                NormalizeList(forLoop.Body.ToList())),
            SwitchOperation sw => OperationTreeMapper.MapSwitchBodies(
                sw,
                bodies => NormalizeList(bodies.ToList())),
            _ => operation,
        };

    private static WhileOperation NormalizeWhile(WhileOperation loop)
    {
        var body = NormalizeLoopBody(loop.Body.ToList());
        return new WhileOperation(loop.Condition, body);
    }

    private static List<Operation> NormalizeLoopBody(List<Operation> body)
    {
        for (var i = 0; i < body.Count; i++)
        {
            if (body[i] is IfOperation branch)
            {
                body[i] = new IfOperation(
                    branch.Condition,
                    NormalizeLoopBody(branch.ThenBody.ToList()),
                    branch.ElseBody is not null ? NormalizeLoopBody(branch.ElseBody.ToList()) : null);
            }
        }

        body = HoistIndexIncrementFromVerboseBranch(body);
        body = SplitVerbosePrefixFromLinePrint(body);
        return body;
    }

    /// <summary>
    /// <c>if (sub(..., 'v')) { verbose = 1; i++; } else { ... }</c> →
    /// <c>if (sub(..., 'v')) { verbose = 1; } else { ... } i++;</c>.
    /// </summary>
    private static List<Operation> HoistIndexIncrementFromVerboseBranch(List<Operation> body)
    {
        for (var i = 0; i < body.Count; i++)
        {
            if (body[i] is not IfOperation branch
                || branch.ElseBody is not { Count: > 0 }
                || !TryExtractIndexIncrement(branch.ThenBody, out var index))
            {
                continue;
            }

            var thenBody = branch.ThenBody.Take(branch.ThenBody.Count - 1).ToList();

            body[i] = new IfOperation(branch.Condition, thenBody, branch.ElseBody);
            body.Insert(i + 1, new IncOperation(index));
            break;
        }

        return body;
    }

    private static bool TryExtractIndexIncrement(IReadOnlyList<Operation> thenBody, out Variable index)
    {
        index = null!;

        if (thenBody.Count == 0)
        {
            return false;
        }

        return thenBody[^1] switch
        {
            IncOperation { Target: Variable inc } => AssignIndex(inc, out index),
            SetOperation
            {
                Dst: Variable dst,
                Src: Math2Expr { Operation: Math2Operation.Add, First: Variable addIndex, Second: ConstExpr { Value: 1 } },
            } when ReferenceEquals(dst, addIndex) => AssignIndex(dst, out index),
            _ => false,
        };
    }

    private static bool AssignIndex(Variable candidate, out Variable index)
    {
        index = candidate;
        return true;
    }

    /// <summary>
    /// <c>if (verbose) { printf("[%d]", n); printf("%s\n", arg); }</c> → префикс + безусловная строка.
    /// </summary>
    private static List<Operation> SplitVerbosePrefixFromLinePrint(List<Operation> body)
    {
        for (var i = 0; i < body.Count; i++)
        {
            if (body[i] is not IfOperation branch
                || branch.ElseBody is { Count: > 0 }
                || !LooksLikeVerboseGuard(branch.Condition)
                || branch.ThenBody.Count < 2
                || branch.ThenBody[^1] is not CallOperation lineCall
                || !LooksLikeLinePrint(lineCall))
            {
                continue;
            }

            var prefixCalls = branch.ThenBody.Take(branch.ThenBody.Count - 1).ToList();
            if (prefixCalls.Count == 0 || prefixCalls[^1] is not CallOperation prefixCall || !LooksLikeVerbosePrefix(prefixCall))
            {
                continue;
            }

            body[i] = new IfOperation(branch.Condition, prefixCalls, null);
            body.Insert(i + 1, lineCall);
            break;
        }

        return body;
    }

    private static bool LooksLikeVerbosePrefix(CallOperation call) =>
        call.Args.Count >= 1 && GetFormatText(call.Args[0]).Contains('[');

    private static bool LooksLikeLinePrint(CallOperation call) =>
        call.Args.Count >= 1 && GetFormatText(call.Args[0]).Contains("%s");

    private static string GetFormatText(Expr arg) =>
        arg switch
        {
            StringExpr { Value: var text } => text,
            _ => arg.ToString(),
        };

    private static bool LooksLikeVerboseGuard(Expr condition) =>
        condition is CmpExpr { Operation: CmpOperation.Ne, Right: ConstExpr { Value: 0 }, Left: Variable };
}

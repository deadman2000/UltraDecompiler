using UltraDecompiler.Decompilation;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Выносит <c>i++</c> из вложенных веток в конец тела цикла по <c>argc</c>.
/// </summary>
public static class ArgvLoopIncrementHoister
{
    /// <summary>Переносит инкремент индекса в конец while по argv.</summary>
    public static IReadOnlyList<Operation> Hoist(IReadOnlyList<Operation> operations) =>
        HoistList(operations.ToList());

    private static List<Operation> HoistList(List<Operation> operations)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = HoistNested(operations[i]);
        }

        for (var i = 0; i < operations.Count; i++)
        {
            if (operations[i] is WhileOperation loop
                && loop.Condition is CmpExpr { Operation: CmpOperation.Ult, Left: Variable index, Right: Variable { Name: "argc" } })
            {
                operations[i] = HoistWhileIncrement(loop, index);
            }
        }

        return operations;
    }

    private static Operation HoistNested(Operation operation) =>
        operation switch
        {
            IfOperation branch => new IfOperation(
                branch.Condition,
                HoistList(branch.ThenBody.ToList()),
                branch.ElseBody is not null ? HoistList(branch.ElseBody.ToList()) : null),
            WhileOperation loop => new WhileOperation(loop.Condition, HoistList(loop.Body.ToList())),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? HoistNested(loop.Init) : null,
                loop.Condition,
                loop.Iteration is not null ? HoistNested(loop.Iteration) : null,
                HoistList(loop.Body.ToList())),
            _ => operation,
        };

    private static WhileOperation HoistWhileIncrement(WhileOperation loop, Variable index)
    {
        IncOperation? increment = null;
        var body = StripIncrement(loop.Body.ToList(), index, ref increment);
        if (increment is null)
        {
            return loop;
        }

        if (body.Count == 0 || body[^1] is not IncOperation)
        {
            body.Add(increment);
        }

        return new WhileOperation(loop.Condition, body);
    }

    private static List<Operation> StripIncrement(List<Operation> ops, Variable index, ref IncOperation? found)
    {
        var result = new List<Operation>(ops.Count);
        foreach (var op in ops)
        {
            if (op is IncOperation { Target: Variable target } && SameVariable(target, index))
            {
                found = new IncOperation(target);
                continue;
            }

            if (op is IfOperation branch)
            {
                result.Add(new IfOperation(
                    branch.Condition,
                    StripIncrement(branch.ThenBody.ToList(), index, ref found),
                    branch.ElseBody is not null
                        ? StripIncrement(branch.ElseBody.ToList(), index, ref found)
                        : null));
                continue;
            }

            result.Add(op);
        }

        return result;
    }

    private static bool SameVariable(Variable left, Variable right) =>
        left.Name is not null && right.Name is not null
            ? left.Name == right.Name
            : ReferenceEquals(left, right);
}

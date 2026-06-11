namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Преобразует <c>if (c) { return a; } else { return b; }</c> в стиль QuickC:
/// <c>if (c) { return a; } return b;</c> — без лишней ветки <c>else</c>.
/// </summary>
public static class IfElseReturnFlattener
{
    /// <summary>Убирает избыточный <c>else</c> у условных return.</summary>
    public static IReadOnlyList<Operation> Flatten(IReadOnlyList<Operation> operations) =>
        FlattenList(operations.ToList());

    private static List<Operation> FlattenList(List<Operation> operations)
    {
        var result = new List<Operation>(operations.Count);

        foreach (var operation in operations)
        {
            if (operation is IfOperation branch
                && branch.ElseBody is { Count: > 0 } elseBody
                && BranchEndsWithReturn(branch.ThenBody)
                && BranchEndsWithReturn(elseBody))
            {
                result.Add(new IfOperation(
                    branch.Condition,
                    FlattenList(branch.ThenBody.ToList()),
                    null));
                result.AddRange(FlattenList(elseBody.ToList()));
                continue;
            }

            result.Add(FlattenNested(operation));
        }

        return result;
    }

    private static Operation FlattenNested(Operation operation) =>
        operation switch
        {
            IfOperation branch => new IfOperation(
                branch.Condition,
                FlattenList(branch.ThenBody.ToList()),
                branch.ElseBody is null ? null : FlattenList(branch.ElseBody.ToList())),
            WhileOperation loop => new WhileOperation(
                loop.Condition,
                FlattenList(loop.Body.ToList())),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? FlattenNested(loop.Init) : null,
                loop.Condition,
                loop.Iteration is not null ? FlattenNested(loop.Iteration) : null,
                FlattenList(loop.Body.ToList())),
            _ => operation,
        };

    private static bool BranchEndsWithReturn(IReadOnlyList<Operation> body) =>
        body.Count > 0 && body[^1] is ReturnOperation;
}

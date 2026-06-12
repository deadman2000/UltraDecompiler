using UltraDecompiler.Decompilation;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Для void-процедур QuickC убирает неявные <see cref="ReturnOperation"/> из IR
/// и нормализует явные return к голой форме без значения AX.
/// </summary>
public static class VoidReturnNormalizer
{
    /// <summary>
    /// Удаляет неявные return и очищает пустые ветки if/else для void-функций.
    /// </summary>
    public static IReadOnlyList<Operation> Normalize(
        DisassembledProcedure procedure,
        IReadOnlyList<Operation> operations)
    {
        if (!procedure.Signature.ReturnType.IsVoid)
        {
            return operations;
        }

        return NormalizeList(operations.ToList());
    }

    private static List<Operation> NormalizeList(List<Operation> operations)
    {
        var result = new List<Operation>(operations.Count);

        foreach (var operation in operations)
        {
            if (operation is ReturnOperation ret)
            {
                if (!ret.IsExplicit)
                {
                    continue;
                }

                result.Add(new ReturnOperation(null, IsExplicit: true));
                continue;
            }

            result.Add(NormalizeNested(operation));
        }

        return result;
    }

    private static Operation NormalizeNested(Operation operation) =>
        operation switch
        {
            IfOperation branch => PruneIf(new IfOperation(
                branch.Condition,
                NormalizeList(branch.ThenBody.ToList()),
                branch.ElseBody is null ? null : NormalizeList(branch.ElseBody.ToList()))),
            WhileOperation loop => new WhileOperation(
                loop.Condition,
                NormalizeList(loop.Body.ToList())),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? NormalizeNested(loop.Init) : null,
                loop.Condition,
                loop.Iteration is not null ? NormalizeNested(loop.Iteration) : null,
                NormalizeList(loop.Body.ToList())),
            _ => operation,
        };

    private static IfOperation PruneIf(IfOperation branch) =>
        branch with
        {
            ElseBody = branch.ElseBody is { Count: 0 } ? null : branch.ElseBody,
        };
}

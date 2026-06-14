namespace UltraDecompiler.PostProcessing.Types;

/// <summary>
/// Восстанавливает локальные <c>char far *</c> из пары стековых слов (offset + segment)
/// и сворачивает инициализацию в литерал <c>(char far *)0xSEG0000L</c>.
/// </summary>
public static class FarPointerLocalInferrer
{
    /// <summary>
    /// Удаляет отдельные присваивания offset/segment и задаёт инициализатор far-указателя.
    /// </summary>
    public static IReadOnlyList<Operation> Infer(
        DisassembledProcedure procedure,
        IReadOnlyList<Operation> operations)
    {
        if (procedure.Expressions is null || procedure.Expressions.Variables.FarPointerLocals.Count == 0)
        {
            return operations;
        }

        var result = new List<Operation>(operations);

        foreach (var (_, baseVariable, segmentVariable) in procedure.Expressions.Variables.FarPointerLocals)
        {
            var offsetInit = TryFindConstAssignment(result, baseVariable);
            var segmentInit = TryFindConstAssignment(result, segmentVariable);
            if (offsetInit is null || segmentInit is null)
            {
                continue;
            }

            baseVariable.FarPointerInitializer = PackFarPointer(segmentInit.Value, offsetInit.Value);
            RemoveConstAssignment(result, baseVariable);
            RemoveConstAssignment(result, segmentVariable);
        }

        return result;
    }

    private static int? TryFindConstAssignment(IReadOnlyList<Operation> operations, Variable variable)
    {
        foreach (var op in operations)
        {
            if (op is SetOperation { Dst: var dst, Src: ConstExpr value } && ReferenceEquals(dst, variable))
            {
                return value.Value;
            }
        }

        return null;
    }

    private static void RemoveConstAssignment(List<Operation> operations, Variable variable)
    {
        operations.RemoveAll(op =>
            op is SetOperation { Dst: var dst, Src: ConstExpr } && ReferenceEquals(dst, variable));
    }

    private static uint PackFarPointer(int segment, int offset) =>
        ((uint)(ushort)segment << 16) | (uint)(ushort)offset;
}

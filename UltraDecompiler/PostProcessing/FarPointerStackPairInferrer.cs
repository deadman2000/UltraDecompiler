using UltraDecompiler.Decompilation;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Регистрирует локальные <c>char far *</c> на стеке по паре слов (offset + segment),
/// даже если QuickC не сгенерировал LES/LDS (типично для циклов с индексацией far-указателя).
/// </summary>
public static class FarPointerStackPairInferrer
{
    /// <summary>
    /// Ищет запись через ES с сегментом из стековой локали и связывает её с соседним словом offset.
    /// </summary>
    public static void Infer(DisassembledProcedure procedure, IEnumerable<Operation> operations)
    {
        if (procedure.Expressions is null)
        {
            return;
        }

        var variables = procedure.Expressions.Variables;

        foreach (var op in ExpressionBuilder.EnumerateNested(operations))
        {
            if (op is not StoreOperation { Segment: Variable segmentVar })
            {
                continue;
            }

            foreach (var (offset, offsetVar) in variables.StackLocals)
            {
                if (offset + 2 != GetSegmentOffset(variables, segmentVar))
                {
                    continue;
                }

                variables.RegisterFarPointerLocal(offset, offsetVar, segmentVar);
            }
        }
    }

    private static int GetSegmentOffset(VariableStorage variables, Variable segmentVar)
    {
        foreach (var (offset, variable) in variables.StackLocals)
        {
            if (ReferenceEquals(variable, segmentVar))
            {
                return offset;
            }
        }

        return int.MinValue;
    }
}

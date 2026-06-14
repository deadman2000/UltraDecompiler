using UltraDecompiler.CodeGeneration;

namespace UltraDecompiler.Decompilation;

/// <summary>Маппинг <see cref="DisassembledProcedure"/> в модель для <see cref="CCodeGenerator"/>.</summary>
public static class ProcedureCodegenMapper
{
    /// <summary>Создаёт модель кодогенерации из декомпилированной процедуры.</summary>
    public static ProcedureCodegenModel ToCodegenModel(this DisassembledProcedure procedure)
    {
        var parameters = procedure.Expressions?.Parameters ?? [];
        var stackLocals = procedure.Expressions?.Variables.StackLocals
            .Select(static e => e.Variable)
            .ToList() ?? [];

        return new ProcedureCodegenModel(
            procedure.Name,
            procedure.Offset,
            procedure.Signature,
            parameters,
            stackLocals);
    }
}

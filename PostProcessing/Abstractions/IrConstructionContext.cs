using UltraDecompiler.Disassembly.Graph;

namespace UltraDecompiler.PostProcessing.Abstractions;

/// <summary>Контекст IR-construction pass после <see cref="ExpressionBuilder.Build"/> / <see cref="ExpressionBuilder.BuildProc"/>.</summary>
public sealed class IrConstructionContext
{
    public required ExpressionBuilder Builder { get; init; }
    public required ControlFlowGraph Graph { get; init; }
    public required DisassembledProcedure Procedure { get; init; }
}

using UltraDecompiler.Common;
using UltraDecompiler.Disassembly.Parser;

namespace UltraDecompiler.PostProcessing.Abstractions;

/// <summary>Контекст одного post-process pass над процедурой.</summary>
public sealed record PostProcessContext
{
    public required DisassembledProcedure Procedure { get; init; }
    public required ProcedureStorage Storage { get; init; }
    public required HeaderCatalog HeaderCatalog { get; init; }
    public required byte[] Image { get; init; }
    public ExeImageLayout? Layout { get; init; }
    public CompilerOptions CompilerOptions { get; init; } = new();
}

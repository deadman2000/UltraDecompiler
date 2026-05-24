using UltraDecompiler.Disassembler;

namespace UltraDecompiler.Graph;

/// <summary>
/// Базовый блок — последовательность инструкций без переходов внутри
/// </summary>
public class BasicBlock
{
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }

    public List<Instruction> Instructions { get; set; } = new();

    // Перезоды

    public int? NextOffset { get; set; }

    public int? ConditionalOffset { get; set; }

    public BasicBlock? NextBlock { get; set; }

    public BasicBlock? ConditionalBlock { get; set; }

    public Expression? Condition { get; set; }

    public override string ToString()
    {
        return $"Block 0x{StartOffset:X6} - 0x{EndOffset:X6} ({Instructions.Count} instr)";
    }
}
namespace UltraDecompiler.Disassembly.Graph;

/// <summary>
/// Базовый блок — последовательность инструкций без переходов внутри
/// </summary>
public class BasicBlock
{
    public int StartOffset { get; set; }

    public int EndOffset { get; set; } = -1;

    public List<Instruction> Instructions { get; set; } = [];

    // Переходы
    public int? NextOffset { get; set; }

    public int? ConditionalOffset { get; set; }

    public BasicBlock? NextBlock { get; set; }

    public BasicBlock? ConditionalBlock { get; set; }

    public override string ToString()
    {
        return $"Block {StartOffset:X6}h - {EndOffset:X6}h ({Instructions.Count} instr)";
    }
}
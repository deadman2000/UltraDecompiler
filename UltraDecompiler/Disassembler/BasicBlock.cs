namespace UltraDecompiler.Disassembler;

/// <summary>
/// Базовый блок — последовательность инструкций без переходов внутри
/// </summary>
public class BasicBlock
{
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public List<Instruction> Instructions { get; set; } = new();

    // Ссылки на другие блоки
    public List<BasicBlock> Successors { get; set; } = new();
    public List<BasicBlock> Predecessors { get; set; } = new();

    public override string ToString()
    {
        return $"Block 0x{StartOffset:X6} - 0x{EndOffset:X6} ({Instructions.Count} instr)";
    }
}
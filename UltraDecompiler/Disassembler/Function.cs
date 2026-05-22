using System.Collections.Generic;

namespace UltraDecompiler.Disassembler;

/// <summary>
/// Представляет одну функцию/процедуру в программе
/// </summary>
public class Function
{
    public int EntryPoint { get; set; }
    public string Name { get; set; } = "sub_????";
    public List<BasicBlock> Blocks { get; set; } = new();
    public List<int> Callers { get; set; } = new();   // адреса, откуда вызывают эту функцию

    public override string ToString()
    {
        return $"{Name} (0x{EntryPoint:X6}) - {Blocks.Count} blocks";
    }
}
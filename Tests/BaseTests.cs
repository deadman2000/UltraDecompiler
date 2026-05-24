using UltraDecompiler.Disassembler;
using UltraDecompiler.Graph;

namespace Tests;

public abstract class BaseTests
{
    protected static List<Instruction> Disassemble(string hex)
    {
        var disassembler = new X86Disassembler(hex.FromHex());
        disassembler.Disassemble(0, RegisterState.Unknown);
        return disassembler.Instructions;
    }

    protected static ControlFlowGraph GetGraph(string hex)
    {
        var disassembler = new X86Disassembler(hex.FromHex());
        disassembler.Disassemble(0, RegisterState.Unknown);

        var graph = new ControlFlowGraph();
        graph.Build(disassembler, 0);
        return graph;
    }
}

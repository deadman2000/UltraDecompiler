using UltraDecompiler.Disassembler;

namespace Tests;

public class DisassemblerTests
{
    [Fact]
    public void DisassembleNearInderectCall()
    {
        var instructions = Disassemble("FF 16 46 00");
        Assert.Equal("CALL", instructions[0].Mnemonic);
        Assert.Equal("DS:0x0046", instructions[0].Operands);
    }

    private static List<Instruction> Disassemble(string hex)
    {
        var disassembler = new X86Disassembler(hex.FromHex());
        disassembler.Disassemble(0);
        return disassembler.Instructions;
    }
}

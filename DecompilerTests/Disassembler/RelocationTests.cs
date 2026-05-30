using DecompilerTests;
using UltraDecompiler.Disassembler;
using UltraDecompiler.Parser;

namespace DecompilerTests.Disassembler;

public class RelocationTests : BaseTests
{
    [Fact]
    public void Disassembler_MarksRelocatedImmediateInOutput()
    {
        // MOV DI, 0166h — слово непосредственного операнда на смещении 1 входит в таблицу релокаций
        byte[] raw = [0xBF, 0x66, 0x01];
        var relocs = new RelocationEntry[] { new() { Offset = 1, Segment = 0 } };

        var disassembler = new X86Disassembler(raw, relocs);
        disassembler.Disassemble(0);

        var instr = disassembler.Instructions[0];
        Assert.Equal(Mnemonic.MOV, instr.Mnemonic);
        Assert.True(instr.Operand2.IsRelocated);
        Assert.Equal("DI, offset 0166h", instr.Operands);
    }

    [Fact]
    public void Disassembler_MarksRelocatedMemoryDisplacement()
    {
        // MOV [1234h], 5678h — disp16 на смещении 2
        byte[] raw = [0xC7, 0x06, 0x34, 0x12, 0x78, 0x56];
        var relocs = new RelocationEntry[] { new() { Offset = 2, Segment = 0 } };

        var disassembler = new X86Disassembler(raw, relocs);
        disassembler.Disassemble(0);

        var instr = disassembler.Instructions[0];
        Assert.True(instr.Operand1.IsRelocated);
        Assert.Equal("[offset 1234h], 5678h", instr.Operands);
    }

    [Fact]
    public void Disassembler_DoesNotMarkNonRelocatedImmediate()
    {
        var disassembler = Disassemble("B8 34 12");
        Assert.False(disassembler[0].Operand2.IsRelocated);
        Assert.Equal("AX, 1234h", disassembler[0].Operands);
    }
}

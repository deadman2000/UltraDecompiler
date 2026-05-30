using Common;
using UltraDecompiler.Disassembler;

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
        Assert.Equal("offset", instr.Operand2.Relocation);
        Assert.Equal("DI, offset+0166h", instr.Operands);
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
        Assert.Equal("offset", instr.Operand1.Relocation);
        Assert.Equal("[offset+1234h], 5678h", instr.Operands);
    }

    [Fact]
    public void Disassembler_DoesNotMarkNonRelocatedImmediate()
    {
        var disassembler = Disassemble("B8 34 12");
        Assert.Null(disassembler[0].Operand2.Relocation);
        Assert.Equal("AX, 1234h", disassembler[0].Operands);
    }

    [Fact]
    public void Disassembler_UsesPerEntryOffsetNames()
    {
        byte[] raw = [0xBF, 0x66, 0x01, 0xC7, 0x06, 0x34, 0x12, 0x78, 0x56];
        var relocs = new RelocationEntry[]
        {
            new() { Offset = 1, Segment = 0, OffsetName = "code" },
            new() { Offset = 5, Segment = 0, OffsetName = "data" },
        };

        var disassembler = new X86Disassembler(raw, new RelocationTable("", relocs));
        disassembler.Disassemble(0);

        Assert.Equal("code", disassembler.Instructions[0].Operand2.Relocation);
        Assert.Equal("DI, code+0166h", disassembler.Instructions[0].Operands);
        Assert.Equal("data", disassembler.Instructions[1].Operand1.Relocation);
        Assert.Equal("[data+1234h], 5678h", disassembler.Instructions[1].Operands);
    }

    [Fact]
    public void Disassembler_MarksRelocatedNearCall()
    {
        // E8 rel16 — слово смещения на offset 1 помечено pc-relative релокацией
        byte[] raw = [0xE8, 0x00, 0x00];
        var relocs = new RelocationEntry[] { new() { Offset = 1, Segment = 0, OffsetName = "__output" } };

        var disassembler = new X86Disassembler(raw, new RelocationTable("", relocs));
        disassembler.Disassemble(0);

        var instr = disassembler.Instructions[0];
        Assert.Equal(Mnemonic.CALL, instr.Mnemonic);
        Assert.Equal("__output", instr.Operand1.Relocation);
        Assert.Equal("__output", instr.Operands);
    }

    [Fact]
    public void Disassembler_DoesNotMarkNonRelocatedNearCall()
    {
        var disassembler = Disassemble("E8 05 00"); // CALL +5 → target 8
        Assert.Equal(Mnemonic.CALL, disassembler[0].Mnemonic);
        Assert.Null(disassembler[0].Operand1.Relocation);
        Assert.Equal("8", disassembler[0].Operands);
    }
}

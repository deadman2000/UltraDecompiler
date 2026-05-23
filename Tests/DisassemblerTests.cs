using UltraDecompiler.Disassembler;

namespace Tests;

public class DisassemblerTests
{
    [Fact]
    public void DisassembleNearInderectCall()
    {
        var instructions = Disassemble("FF 16 46 00");
        Assert.Equal(Mnemonic.CALL, instructions[0].Mnemonic);
        Assert.Equal("[0046h]", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleDirectNearJump()
    {
        var instructions = Disassemble("E9 05 00"); // JMP +5
        Assert.Equal(Mnemonic.JMP, instructions[0].Mnemonic);
        Assert.Equal("8", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleDirectShortJump()
    {
        var instructions = Disassemble("EB 05"); // JMP SHORT +5
        Assert.Equal(Mnemonic.JMP, instructions[0].Mnemonic);
        Assert.Equal("7", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleIndirectJumpWithBaseIndex()
    {
        var instructions = Disassemble("FF 27"); // JMP WORD PTR [BX]
        Assert.Equal(Mnemonic.JMP, instructions[0].Mnemonic);
        Assert.Contains("BX", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleComplexIndirectCall()
    {
        // CALL WORD PTR [BX+SI+1234]
        var instructions = Disassemble("FF 90 34 12");
        Assert.Equal(Mnemonic.CALL, instructions[0].Mnemonic);
        Assert.Contains("BX+SI", instructions[0].Operands);
        Assert.Contains("1234", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleFarIndirectJump()
    {
        // JMP DWORD PTR [1234] (far jump through memory)
        var instructions = Disassemble("FF 2E 34 12");
        Assert.Equal(Mnemonic.JMP, instructions[0].Mnemonic);
        Assert.Contains("1234", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleMovWithComplexMemory()
    {
        // MOV AX, [BP+DI+5]
        var instructions = Disassemble("8B 43 05");
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("AX, [BP+DI+5]", instructions[0].Operands);
    }

    [Fact]
    public void DisassemblePushMemory()
    {
        // PUSH WORD PTR [1234]
        var instructions = Disassemble("FF 36 34 12");
        Assert.Equal(Mnemonic.PUSH, instructions[0].Mnemonic);
        Assert.Contains("1234", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleWithSegmentOverride()
    {
        // ES: MOV AX, [1234]
        var instructions = Disassemble("26 8B 06 34 12");
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Contains("ES:", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleLockPrefix()
    {
        // LOCK INC WORD PTR [BX]
        var instructions = Disassemble("F0 FF 07");
        Assert.Equal(InstructionPrefix.LOCK, instructions[0].Prefix);
        Assert.Equal(Mnemonic.INC, instructions[0].Mnemonic);
        Assert.Contains("BX", instructions[0].Operands);
        Assert.Single(instructions);
    }

    [Fact]
    public void DisassembleRepzPrefix()
    {
        // REPZ MOVSB
        var instructions = Disassemble("F3 A4");
        Assert.Equal(InstructionPrefix.REPZ, instructions[0].Prefix);
        Assert.Equal(Mnemonic.MOVSB, instructions[0].Mnemonic);
    }

    [Fact]
    public void DisassembleRepnzPrefix()
    {
        // REPNZ CMPSB
        var instructions = Disassemble("F2 A6");
        Assert.Equal(InstructionPrefix.REPNZ, instructions[0].Prefix);
        Assert.Equal(Mnemonic.CMPSB, instructions[0].Mnemonic);
    }

    [Fact]
    public void DisassembleLockWithSegmentOverride()
    {
        // LOCK ES: INC WORD PTR [BX]
        var instructions = Disassemble("F0 26 FF 07");
        Assert.Equal(InstructionPrefix.LOCK, instructions[0].Prefix);
        Assert.Equal(Segment.ES, instructions[0].Segment);
        Assert.Equal(Mnemonic.INC, instructions[0].Mnemonic);
    }

    [Fact]
    public void DisassembleRepzWithSegmentOverride()
    {
        // REPZ SS: MOVSW
        var instructions = Disassemble("F3 36 A5");
        Assert.Equal(InstructionPrefix.REPZ, instructions[0].Prefix);
        Assert.Equal(Segment.SS, instructions[0].Segment);
        Assert.Equal(Mnemonic.MOVSW, instructions[0].Mnemonic);
    }

    [Fact]
    public void DisassembleMultiplePrefixes()
    {
        // LOCK REPNZ SCASW (редкий, но валидный случай)
        var instructions = Disassemble("F0 F2 AE");
        Assert.Equal(InstructionPrefix.LOCK | InstructionPrefix.REPNZ, instructions[0].Prefix);
        Assert.Equal(Mnemonic.SCASB, instructions[0].Mnemonic);
    }

    [Fact]
    public void SegmentChange()
    {
        var instructions = Disassemble("""
            36 FF 36 DA 00;  push word ptr ss:[0xda]
            FF 36 DA 00;     push word ptr ds:[0xda]
            """);
        Assert.Equal(2, instructions.Count);
        Assert.Equal(Segment.SS, instructions[0].Segment);
        Assert.Equal(Segment.None, instructions[1].Segment);
        Assert.StartsWith("SS:", instructions[0].Operands);
        Assert.Equal("[00DAh]", instructions[1].Operands);
    }

    private static List<Instruction> Disassemble(string hex)
    {
        var disassembler = new X86Disassembler(hex.FromHex());
        disassembler.Disassemble(0);
        return disassembler.Instructions;
    }
}

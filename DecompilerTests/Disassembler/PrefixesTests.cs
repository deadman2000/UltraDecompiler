using UltraDecompiler.Disassembler;

namespace DecompilerTests.Disassembler;

public class PrefixesTests : BaseTests
{
    [Fact]
    public void DisassembleWithSegmentOverride()
    {
        // ES: MOV AX, [1234]
        var instructions = Disassemble("26 8B 06 34 12"); // ES: MOV AX, [1234h]
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Contains("ES:", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(0x1234, instructions[0].Operand2.Value);
        Assert.Equal(AddressRegister.None, instructions[0].Operand2.BaseReg);
        Assert.Equal(AddressRegister.None, instructions[0].Operand2.IndexReg);
    }

    [Fact]
    public void DisassembleLockPrefix()
    {
        // LOCK INC [BX]
        var instructions = Disassemble("F0 FF 07"); // LOCK INC WORD PTR [BX]
        Assert.Equal(InstructionPrefix.LOCK, instructions[0].Prefix);
        Assert.Equal(Mnemonic.INC, instructions[0].Mnemonic);
        Assert.Contains("BX", instructions[0].Operands);
        Assert.Single(instructions);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand1.BaseReg); // BX
        Assert.Equal(AddressRegister.None, instructions[0].Operand1.IndexReg);
    }

    [Fact]
    public void DisassembleRepzPrefix()
    {
        // REPZ MOVSB
        var instructions = Disassemble("F3 A4"); // REP MOVSB
        Assert.Equal(InstructionPrefix.REPZ, instructions[0].Prefix);
        Assert.Equal(Mnemonic.MOVSB, instructions[0].Mnemonic);
    }

    [Fact]
    public void DisassembleRepnzPrefix()
    {
        // REPNZ CMPSB
        var instructions = Disassemble("F2 A6"); // REPNZ CMPSB
        Assert.Equal(InstructionPrefix.REPNZ, instructions[0].Prefix);
        Assert.Equal(Mnemonic.CMPSB, instructions[0].Mnemonic);
    }

    [Fact]
    public void DisassembleLockWithSegmentOverride()
    {
        // LOCK ES: INC [BX]
        var instructions = Disassemble("F0 26 FF 07"); // LOCK ES: INC WORD PTR [BX]
        Assert.Equal(InstructionPrefix.LOCK, instructions[0].Prefix);
        Assert.Equal(Segment.ES, instructions[0].Segment);
        Assert.Equal(Mnemonic.INC, instructions[0].Mnemonic);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand1.BaseReg);
    }

    [Fact]
    public void DisassembleRepzWithSegmentOverride()
    {
        // REPZ SS: MOVSW
        var instructions = Disassemble("F3 36 A5"); // REP SS: MOVSW
        Assert.Equal(InstructionPrefix.REPZ, instructions[0].Prefix);
        Assert.Equal(Segment.SS, instructions[0].Segment);
        Assert.Equal(Mnemonic.MOVSW, instructions[0].Mnemonic);
    }

    [Fact]
    public void DisassembleMultiplePrefixes()
    {
        // LOCK REPNZ SCASW (редкий, но валидный случай)
        var instructions = Disassemble("F0 F2 AE"); // LOCK REPNZ SCASB
        Assert.Equal(InstructionPrefix.LOCK | InstructionPrefix.REPNZ, instructions[0].Prefix);
        Assert.Equal(Mnemonic.SCASB, instructions[0].Mnemonic);
    }

    [Fact]
    public void DisassembSegmentChange()
    {
        var instructions = Disassemble("""
            36 FF 36 DA 00;  push ss:[00DAh]
            FF 36 DA 00;     push [00DAh]
            """);
        Assert.Equal(2, instructions.Count);

        Assert.Equal(Segment.SS, instructions[0].Segment);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(0x00DA, instructions[0].Operand1.Value);
        Assert.Equal(AddressRegister.None, instructions[0].Operand1.BaseReg);

        Assert.Equal(Segment.None, instructions[1].Segment);
        Assert.Equal(OperandType.Memory, instructions[1].Operand1.Type);
        Assert.Equal(0x00DA, instructions[1].Operand1.Value);
        Assert.Equal(AddressRegister.None, instructions[1].Operand1.BaseReg);

        Assert.StartsWith("SS:", instructions[0].Operands);
        Assert.Equal("[DAh]", instructions[1].Operands);
    }

    [Fact]
    public void DisassembleWithCsSegmentOverride()
    {
        var instructions = Disassemble("2E 8B 06 34 12"); // CS: MOV AX, [1234]
        Assert.Equal(Segment.CS, instructions[0].Segment);
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Contains("CS:", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleWithDsSegmentOverride()
    {
        var instructions = Disassemble("3E 8B 06 34 12"); // DS: MOV AX, [1234]
        Assert.Equal(Segment.DS, instructions[0].Segment);
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Contains("DS:", instructions[0].Operands);
    }
}

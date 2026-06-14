namespace DecompilerTests.Disassembler;

public class MovTests : BaseTests
{
    [Fact]
    public void DisassembleMovImmediate()
    {
        var instructions = Disassemble("B8 34 12"); // MOV AX, 1234h
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("AX, 1234h", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate16, instructions[0].Operand2.Type);
        Assert.Equal(0x1234, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleMovMemImmByte()
    {
        var instructions = Disassemble("C6 06 34 12 55"); // MOV BYTE PTR [1234h], 55h
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("[1234h], 55h", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(0x1234, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand2.Type);
        Assert.Equal(0x55, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleMovMemImmWord()
    {
        var instructions = Disassemble("C7 06 34 12 78 56"); // MOV [1234h], 5678h
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("[1234h], 5678h", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(0x1234, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate16, instructions[0].Operand2.Type);
        Assert.Equal(0x5678, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleMovAxMemByte()
    {
        var instructions = Disassemble("A0 34 12"); // MOV AL, [1234h]
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("AL, [1234h]", instructions[0].Operands);
        Assert.Equal(OperandType.Register8, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(0x1234, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleMovAxMemWord()
    {
        var instructions = Disassemble("A1 34 12"); // MOV AX, [1234h]
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("AX, [1234h]", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(0x1234, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleMovMemAxByte()
    {
        var instructions = Disassemble("A2 34 12"); // MOV [1234h], AL
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("[1234h], AL", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(0x1234, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(0, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleMovMemAxWord()
    {
        var instructions = Disassemble("A3 34 12"); // MOV [1234h], AX
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("[1234h], AX", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(0x1234, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Register16, instructions[0].Operand2.Type);
        Assert.Equal(0, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleMovSregToReg()
    {
        var instructions = Disassemble("8C D8"); // MOV AX, DS
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("AX, DS", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.SegmentRegister, instructions[0].Operand2.Type);
        Assert.Equal(3, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleMovRegToSreg()
    {
        var instructions = Disassemble("8E D8"); // MOV DS, AX
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("DS, AX", instructions[0].Operands);
        Assert.Equal(OperandType.SegmentRegister, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Register16, instructions[0].Operand2.Type);
        Assert.Equal(0, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleMovWithComplexMemory()
    {
        var instructions = Disassemble("8B 43 05"); // MOV AX, [BP+DI+5]
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("AX, [BP+DI+5]", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(5, instructions[0].Operand2.Value);
        Assert.Equal(AddressRegister.BP, instructions[0].Operand2.BaseReg); // BP
        Assert.Equal(AddressRegister.DI, instructions[0].Operand2.IndexReg); // DI
    }

    [Fact]
    public void DisassembleMemoryBxDi()
    {
        var instructions = Disassemble("8B 01"); // MOV AX, [BX+DI]
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand2.BaseReg);
        Assert.Equal(AddressRegister.DI, instructions[0].Operand2.IndexReg);
    }

    [Fact]
    public void DisassembleMemoryBpSi()
    {
        var instructions = Disassemble("8B 0A"); // MOV CX, [BP+SI]
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(AddressRegister.BP, instructions[0].Operand2.BaseReg);
        Assert.Equal(AddressRegister.SI, instructions[0].Operand2.IndexReg);
    }

    [Fact]
    public void DisassembleMemoryDi()
    {
        var instructions = Disassemble("8B 0D"); // MOV CX, [DI]
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(AddressRegister.DI, instructions[0].Operand2.BaseReg);
        Assert.Equal(AddressRegister.None, instructions[0].Operand2.IndexReg);
    }

    [Fact]
    public void DisassembleMemoryBpDisp()
    {
        var instructions = Disassemble("8B 4E 05"); // MOV CX, [BP+5]
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(AddressRegister.BP, instructions[0].Operand2.BaseReg);
        Assert.Equal(5, instructions[0].Operand2.Value);
    }

}

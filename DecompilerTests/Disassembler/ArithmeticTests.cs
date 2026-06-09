using UltraDecompiler.Disassembler;

namespace DecompilerTests.Disassembler;

public class ArithmeticTests : BaseTests
{
    [Fact]
    public void DisassembleAddRegImm8()
    {
        var instructions = Disassemble("04 05"); // ADD AL, 5
        Assert.Equal(Mnemonic.ADD, instructions[0].Mnemonic);
        Assert.Equal("AL, 5", instructions[0].Operands);
        Assert.Equal(OperandType.Register8, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand2.Type);
        Assert.Equal(5, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleSubRegReg()
    {
        var instructions = Disassemble("29 CB"); // SUB BX, CX
        Assert.Equal(Mnemonic.SUB, instructions[0].Mnemonic);
        Assert.Equal("BX, CX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Register16, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleIncBytePtr()
    {
        var instructions = Disassemble("FE 07"); // INC BYTE PTR [BX]
        Assert.Equal(Mnemonic.INC, instructions[0].Mnemonic);
        Assert.Contains("BX", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand1.BaseReg);
        Assert.Equal(AddressRegister.None, instructions[0].Operand1.IndexReg);
    }

    [Fact]
    public void DisassembleMulReg()
    {
        var instructions = Disassemble("F7 E1"); // MUL CX
        Assert.Equal(Mnemonic.MUL, instructions[0].Mnemonic);
        Assert.Equal("CX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(1, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleDecReg()
    {
        var instructions = Disassemble("4B"); // DEC BX
        Assert.Equal(Mnemonic.DEC, instructions[0].Mnemonic);
        Assert.Equal("BX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value); // BX
    }

    [Fact]
    public void DisassembleIncReg()
    {
        var instructions = Disassemble("40"); // INC AX
        Assert.Equal(Mnemonic.INC, instructions[0].Mnemonic);
        Assert.Equal("AX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
    }

    [Fact]
    public void DisassembleAdcAlImm()
    {
        var instructions = Disassemble("14 05"); // ADC AL, 5
        Assert.Equal(Mnemonic.ADC, instructions[0].Mnemonic);
        Assert.Equal("AL, 5", instructions[0].Operands);
        Assert.Equal(OperandType.Register8, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand2.Type);
        Assert.Equal(5, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleSbbBxCx()
    {
        var instructions = Disassemble("19 CB"); // SBB BX, CX
        Assert.Equal(Mnemonic.SBB, instructions[0].Mnemonic);
        Assert.Equal("BX, CX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Register16, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleDivReg()
    {
        var instructions = Disassemble("F7 F1"); // DIV CX
        Assert.Equal(Mnemonic.DIV, instructions[0].Mnemonic);
        Assert.Equal("CX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(1, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleIdivReg()
    {
        var instructions = Disassemble("F7 F9"); // IDIV CX
        Assert.Equal(Mnemonic.IDIV, instructions[0].Mnemonic);
        Assert.Equal("CX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(1, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleImulReg()
    {
        var instructions = Disassemble("F7 E9"); // IMUL CX
        Assert.Equal(Mnemonic.IMUL, instructions[0].Mnemonic);
        Assert.Equal("CX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(1, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleNegReg()
    {
        var instructions = Disassemble("F7 D8"); // NEG AX
        Assert.Equal(Mnemonic.NEG, instructions[0].Mnemonic);
        Assert.Equal("AX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleGroup80AddBytePtrImm()
    {
        var instructions = Disassemble("80 07 05"); // ADD BYTE PTR [BX], 5
        Assert.Equal(Mnemonic.ADD, instructions[0].Mnemonic);
        Assert.Equal("[BX], 5", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand1.BaseReg);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand2.Type);
        Assert.Equal(5, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleGroup81AddWordPtrImm()
    {
        var instructions = Disassemble("81 07 34 12"); // ADD [BX], 1234h
        Assert.Equal(Mnemonic.ADD, instructions[0].Mnemonic);
        Assert.Equal("[BX], 1234h", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand1.BaseReg);
        Assert.Equal(OperandType.Immediate16, instructions[0].Operand2.Type);
        Assert.Equal(0x1234, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleGroup83AddBytePtrImmSignExtend()
    {
        var instructions = Disassemble("83 07 FF"); // ADD [BX], -1 (sign extend)
        Assert.Equal(Mnemonic.ADD, instructions[0].Mnemonic);
        Assert.Equal("[BX], FFFFh", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand1.BaseReg);
        Assert.Equal(OperandType.Immediate16, instructions[0].Operand2.Type);
        Assert.Equal(0xFFFF, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleAdcWordPtrBpDisp8()
    {
        var instructions = Disassemble("11 56 FC"); // ADC [BP-4], DX
        Assert.Equal(Mnemonic.ADC, instructions[0].Mnemonic);
        Assert.Equal("[BP-4], DX", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(AddressRegister.BP, instructions[0].Operand1.BaseReg);
        Assert.Equal(AddressRegister.None, instructions[0].Operand1.IndexReg);
        Assert.Equal(-4, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Register16, instructions[0].Operand2.Type);
        Assert.Equal(2, instructions[0].Operand2.Value); // DX
    }

}

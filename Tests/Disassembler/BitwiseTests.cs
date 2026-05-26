using UltraDecompiler.Disassembler;

namespace Tests.Disassembler;

public class BitwiseTests : BaseTests
{
    [Fact]
    public void DisassembleAndRegImm()
    {
        var instructions = Disassemble("25 FF 00"); // AND AX, 00FFh
        Assert.Equal(Mnemonic.AND, instructions[0].Mnemonic);
        Assert.Equal("AX, FFh", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate16, instructions[0].Operand2.Type);
        Assert.Equal(0x00FF, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleXorBxAx()
    {
        var instructions = Disassemble("31 C3"); // XOR BX, AX
        Assert.Equal(Mnemonic.XOR, instructions[0].Mnemonic);
        Assert.Equal("BX, AX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Register16, instructions[0].Operand2.Type);
        Assert.Equal(0, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleTestAlImm()
    {
        var instructions = Disassemble("A8 01"); // TEST AL, 1
        Assert.Equal(Mnemonic.TEST, instructions[0].Mnemonic);
        Assert.Equal("AL, 1", instructions[0].Operands);
        Assert.Equal(OperandType.Register8, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleOrAxImm()
    {
        var instructions = Disassemble("0D FF 00"); // OR AX, 00FFh
        Assert.Equal(Mnemonic.OR, instructions[0].Mnemonic);
        Assert.Equal("AX, FFh", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate16, instructions[0].Operand2.Type);
        Assert.Equal(0x00FF, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleNotReg()
    {
        var instructions = Disassemble("F7 D0"); // NOT AX
        Assert.Equal(Mnemonic.NOT, instructions[0].Mnemonic);
        Assert.Equal("AX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleRolRegCl()
    {
        var instructions = Disassemble("D3 C0"); // ROL AX, CL
        Assert.Equal(Mnemonic.ROL, instructions[0].Mnemonic);
        Assert.Equal("AX, CL", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value); // CL
    }

    [Fact]
    public void DisassembleRorRegCl()
    {
        var instructions = Disassemble("D3 C8"); // ROR AX, CL
        Assert.Equal(Mnemonic.ROR, instructions[0].Mnemonic);
        Assert.Equal("AX, CL", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value); // CL
    }

    [Fact]
    public void DisassembleRclRegCl()
    {
        var instructions = Disassemble("D3 D0"); // RCL AX, CL
        Assert.Equal(Mnemonic.RCL, instructions[0].Mnemonic);
        Assert.Equal("AX, CL", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value); // CL
    }

    [Fact]
    public void DisassembleRcrRegCl()
    {
        var instructions = Disassemble("D3 D8"); // RCR AX, CL
        Assert.Equal(Mnemonic.RCR, instructions[0].Mnemonic);
        Assert.Equal("AX, CL", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value); // CL
    }

    [Fact]
    public void DisassembleShrAxCl()
    {
        var instructions = Disassemble("D3 E8"); // SHR AX, CL
        Assert.Equal(Mnemonic.SHR, instructions[0].Mnemonic);
        Assert.Equal("AX, CL", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value); // CL
    }

    [Fact]
    public void DisassembleSalAxCl()
    {
        var instructions = Disassemble("D3 E0"); // SAL AX, CL (same as SHL)
        Assert.Equal(Mnemonic.SAL, instructions[0].Mnemonic);
        Assert.Equal("AX, CL", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value); // CL
    }

    [Fact]
    public void DisassembleSarAxCl()
    {
        var instructions = Disassemble("D3 F8"); // SAR AX, CL
        Assert.Equal(Mnemonic.SAR, instructions[0].Mnemonic);
        Assert.Equal("AX, CL", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value); // CL
    }

    [Fact]
    public void DisassembleTestModRmByte()
    {
        var instructions = Disassemble("84 07"); // TEST [BX], AL
        Assert.Equal(Mnemonic.TEST, instructions[0].Mnemonic);
        Assert.Equal("[BX], AL", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand1.BaseReg);
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(0, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleTestModRmWord()
    {
        var instructions = Disassemble("85 07"); // TEST [BX], AX
        Assert.Equal(Mnemonic.TEST, instructions[0].Mnemonic);
        Assert.Equal("[BX], AX", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand1.BaseReg);
        Assert.Equal(OperandType.Register16, instructions[0].Operand2.Type);
        Assert.Equal(0, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleGroupF6TestImm()
    {
        var instructions = Disassemble("F6 07 05"); // TEST BYTE PTR [BX], 5
        Assert.Equal(Mnemonic.TEST, instructions[0].Mnemonic);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand2.Type);
        Assert.Equal(5, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleTestModRmReg()
    {
        var instructions = Disassemble("84 C3"); // TEST BL, AL
        Assert.Equal(Mnemonic.TEST, instructions[0].Mnemonic);
        Assert.Equal(OperandType.Register8, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value); // BL
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(0, instructions[0].Operand2.Value); // AL
    }

    [Fact]
    public void DisassembleShiftImm1()
    {
        var instructions = Disassemble("D0 C0"); // ROL AL, 1
        Assert.Equal(Mnemonic.ROL, instructions[0].Mnemonic);
        Assert.Equal(OperandType.Register8, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value);
    }

}

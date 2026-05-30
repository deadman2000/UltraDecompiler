using UltraDecompiler.Disassembler;

namespace DecompilerTests.Disassembler;

public class MemoryTests : BaseTests
{
    [Fact]
    public void DisassembleCmpRegImm()
    {
        var instructions = Disassemble("3C 00"); // CMP AL, 0
        Assert.Equal(Mnemonic.CMP, instructions[0].Mnemonic);
        Assert.Equal("AL, 0", instructions[0].Operands);
        Assert.Equal(OperandType.Register8, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand2.Type);
        Assert.Equal(0, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleLea()
    {
        var instructions = Disassemble("8D 5C 0A"); // LEA BX, [SI+0Ah]
        Assert.Equal(Mnemonic.LEA, instructions[0].Mnemonic);
        Assert.Equal("BX, [SI+0Ah]", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(0x0A, instructions[0].Operand2.Value);
        Assert.Equal(AddressRegister.SI, instructions[0].Operand2.BaseReg);
        Assert.Equal(AddressRegister.None, instructions[0].Operand2.IndexReg);
    }

    [Fact]
    public void DisassembleXchgAxReg()
    {
        var instructions = Disassemble("92"); // XCHG AX, DX
        Assert.Equal(Mnemonic.XCHG, instructions[0].Mnemonic);
        Assert.Equal("AX, DX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Register16, instructions[0].Operand2.Type);
        Assert.Equal(2, instructions[0].Operand2.Value); // DX
    }

    [Fact]
    public void DisassembleXchgRegMem()
    {
        var instructions = Disassemble("87 07"); // XCHG AX, [BX]
        Assert.Equal(Mnemonic.XCHG, instructions[0].Mnemonic);
        Assert.Contains("BX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand2.BaseReg);
    }

    [Fact]
    public void DisassembleDaa()
    {
        var instructions = Disassemble("27"); // DAA
        Assert.Equal(Mnemonic.DAA, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleCbw()
    {
        var instructions = Disassemble("98"); // CBW
        Assert.Equal(Mnemonic.CBW, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleInAlImm()
    {
        var instructions = Disassemble("E4 21"); // IN AL, 21h
        Assert.Equal(Mnemonic.IN, instructions[0].Mnemonic);
        Assert.Equal("AL, 21h", instructions[0].Operands);
        Assert.Equal(OperandType.Register8, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand2.Type);
        Assert.Equal(0x21, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleOutImmAl()
    {
        var instructions = Disassemble("E6 21"); // OUT 21h, AL
        Assert.Equal(Mnemonic.OUT, instructions[0].Mnemonic);
        Assert.Contains("21h", instructions[0].Operands);
        Assert.Contains("AL", instructions[0].Operands);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand1.Type);
        Assert.Equal(0x21, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(0, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleLds()
    {
        var instructions = Disassemble("C5 1E 34 12"); // LDS BX, [1234]
        Assert.Equal(Mnemonic.LDS, instructions[0].Mnemonic);
        Assert.Contains("BX", instructions[0].Operands);
        Assert.Contains("1234", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(0x1234, instructions[0].Operand2.Value);
        Assert.Equal(AddressRegister.None, instructions[0].Operand2.BaseReg);
        Assert.Equal(AddressRegister.None, instructions[0].Operand2.IndexReg);
    }

    [Fact]
    public void DisassembleXlat()
    {
        var instructions = Disassemble("D7"); // XLAT
        Assert.Equal(Mnemonic.XLAT, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleAaa()
    {
        var instructions = Disassemble("37"); // AAA
        Assert.Equal(Mnemonic.AAA, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleAad()
    {
        var instructions = Disassemble("D5 0A"); // AAD
        Assert.Equal(Mnemonic.AAD, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleAam()
    {
        var instructions = Disassemble("D4 0A"); // AAM
        Assert.Equal(Mnemonic.AAM, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleAas()
    {
        var instructions = Disassemble("3F"); // AAS
        Assert.Equal(Mnemonic.AAS, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleDas()
    {
        var instructions = Disassemble("2F"); // DAS
        Assert.Equal(Mnemonic.DAS, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleCwd()
    {
        var instructions = Disassemble("99"); // CWD
        Assert.Equal(Mnemonic.CWD, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleHlt()
    {
        var instructions = Disassemble("F4"); // HLT
        Assert.Equal(Mnemonic.HLT, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleInto()
    {
        var instructions = Disassemble("CE"); // INTO
        Assert.Equal(Mnemonic.INTO, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleLes()
    {
        var instructions = Disassemble("C4 1E 34 12"); // LES BX, [1234]
        Assert.Equal(Mnemonic.LES, instructions[0].Mnemonic);
        Assert.Contains("BX", instructions[0].Operands);
        Assert.Contains("1234", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(0x1234, instructions[0].Operand2.Value);
        Assert.Equal(AddressRegister.None, instructions[0].Operand2.BaseReg);
        Assert.Equal(AddressRegister.None, instructions[0].Operand2.IndexReg);
    }

    [Fact]
    public void DisassembleLeaRegReg()
    {
        var instructions = Disassemble("8D D8"); // LEA BX, AX
        Assert.Equal(Mnemonic.LEA, instructions[0].Mnemonic);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Register16, instructions[0].Operand2.Type);
        Assert.Equal(0, instructions[0].Operand2.Value);
    }

}

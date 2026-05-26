using UltraDecompiler.Disassembler;

namespace Tests.Disassembler;

public class PushPopTests : BaseTests
{
    [Fact]
    public void DisassemblePushMemory()
    {
        var instructions = Disassemble("FF 36 34 12"); // PUSH WORD PTR [1234h]
        Assert.Equal(Mnemonic.PUSH, instructions[0].Mnemonic);
        Assert.Contains("1234", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(0x1234, instructions[0].Operand1.Value);
        Assert.Equal(AddressRegister.None, instructions[0].Operand1.BaseReg);
        Assert.Equal(AddressRegister.None, instructions[0].Operand1.IndexReg);
    }

    [Fact]
    public void DisassemblePushReg16()
    {
        var instructions = Disassemble("50"); // PUSH AX
        Assert.Equal(Mnemonic.PUSH, instructions[0].Mnemonic);
        Assert.Equal("AX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX = 0
    }

    [Fact]
    public void DisassemblePushReg16_BX()
    {
        var instructions = Disassemble("53"); // PUSH BX
        Assert.Equal(Mnemonic.PUSH, instructions[0].Mnemonic);
        Assert.Equal("BX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value); // BX = 3
    }

    [Fact]
    public void DisassemblePopReg16()
    {
        var instructions = Disassemble("58"); // POP AX
        Assert.Equal(Mnemonic.POP, instructions[0].Mnemonic);
        Assert.Equal("AX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX = 0
    }

    [Fact]
    public void DisassemblePopReg16_BP()
    {
        var instructions = Disassemble("5D"); // POP BP
        Assert.Equal(Mnemonic.POP, instructions[0].Mnemonic);
        Assert.Equal("BP", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(5, instructions[0].Operand1.Value); // BP = 5
    }

    [Fact]
    public void DisassemblePushSegment()
    {
        var instructions = Disassemble("06"); // PUSH ES
        Assert.Equal(Mnemonic.PUSH, instructions[0].Mnemonic);
        Assert.Equal("ES", instructions[0].Operands);
        Assert.Equal(OperandType.SegmentRegister, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // ES = 0
    }

    [Fact]
    public void DisassemblePopSegment()
    {
        var instructions = Disassemble("07"); // POP ES
        Assert.Equal(Mnemonic.POP, instructions[0].Mnemonic);
        Assert.Equal("ES", instructions[0].Operands);
        Assert.Equal(OperandType.SegmentRegister, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // ES = 0
    }

    [Fact]
    public void DisassemblePushf()
    {
        var instructions = Disassemble("9C"); // PUSHF
        Assert.Equal(Mnemonic.PUSHF, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassemblePushCs()
    {
        var instructions = Disassemble("0E"); // PUSH CS
        Assert.Equal(Mnemonic.PUSH, instructions[0].Mnemonic);
        Assert.Equal("CS", instructions[0].Operands);
        Assert.Equal(OperandType.SegmentRegister, instructions[0].Operand1.Type);
        Assert.Equal(1, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassemblePushSs()
    {
        var instructions = Disassemble("16"); // PUSH SS
        Assert.Equal(Mnemonic.PUSH, instructions[0].Mnemonic);
        Assert.Equal("SS", instructions[0].Operands);
        Assert.Equal(OperandType.SegmentRegister, instructions[0].Operand1.Type);
        Assert.Equal(2, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassemblePushDs()
    {
        var instructions = Disassemble("1E"); // PUSH DS
        Assert.Equal(Mnemonic.PUSH, instructions[0].Mnemonic);
        Assert.Equal("DS", instructions[0].Operands);
        Assert.Equal(OperandType.SegmentRegister, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassemblePopSs()
    {
        var instructions = Disassemble("17"); // POP SS
        Assert.Equal(Mnemonic.POP, instructions[0].Mnemonic);
        Assert.Equal("SS", instructions[0].Operands);
        Assert.Equal(OperandType.SegmentRegister, instructions[0].Operand1.Type);
        Assert.Equal(2, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassemblePopDs()
    {
        var instructions = Disassemble("1F"); // POP DS
        Assert.Equal(Mnemonic.POP, instructions[0].Mnemonic);
        Assert.Equal("DS", instructions[0].Operands);
        Assert.Equal(OperandType.SegmentRegister, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value);
    }

}

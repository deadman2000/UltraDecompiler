using UltraDecompiler.Disassembler;

namespace Tests.Disassembler;

public class StringOpsTests : BaseTests
{
    [Fact]
    public void DisassembleMovsb()
    {
        var instructions = Disassemble("A4"); // MOVSB
        Assert.Equal(Mnemonic.MOVSB, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleCmpsw()
    {
        var instructions = Disassemble("A7"); // CMPSW
        Assert.Equal(Mnemonic.CMPSW, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleLodsb()
    {
        var instructions = Disassemble("AC"); // LODSB
        Assert.Equal(Mnemonic.LODSB, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleStosb()
    {
        var instructions = Disassemble("AA"); // STOSB
        Assert.Equal(Mnemonic.STOSB, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleScasw()
    {
        var instructions = Disassemble("AF"); // SCASW
        Assert.Equal(Mnemonic.SCASW, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleStosw()
    {
        var instructions = Disassemble("AB"); // STOSW
        Assert.Equal(Mnemonic.STOSW, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleLodsw()
    {
        var instructions = Disassemble("AD"); // LODSW
        Assert.Equal(Mnemonic.LODSW, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleScasb()
    {
        var instructions = Disassemble("AE"); // SCASB
        Assert.Equal(Mnemonic.SCASB, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
    }

}

using UltraDecompiler.Disassembler;

namespace DecompilerTests.Disassembler;

/// <summary>
/// Тесты дизассемблера для инструкций работы с флагами.
/// Все методы перенесены полностью со всеми оригинальными Assert-проверками.
/// </summary>
public class FlagTests : BaseTests
{
    [Fact]
    public void DisassembleLahf()
    {
        var instructions = Disassemble("9F"); // LAHF
        Assert.Equal(Mnemonic.LAHF, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleSahf()
    {
        var instructions = Disassemble("9E"); // SAHF
        Assert.Equal(Mnemonic.SAHF, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleClc()
    {
        var instructions = Disassemble("F8"); // CLC
        Assert.Equal(Mnemonic.CLC, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleCld()
    {
        var instructions = Disassemble("FC"); // CLD
        Assert.Equal(Mnemonic.CLD, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleCli()
    {
        var instructions = Disassemble("FA"); // CLI
        Assert.Equal(Mnemonic.CLI, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleCmc()
    {
        var instructions = Disassemble("F5"); // CMC
        Assert.Equal(Mnemonic.CMC, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleStc()
    {
        var instructions = Disassemble("F9"); // STC
        Assert.Equal(Mnemonic.STC, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleStd()
    {
        var instructions = Disassemble("FD"); // STD
        Assert.Equal(Mnemonic.STD, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleSti()
    {
        var instructions = Disassemble("FB"); // STI
        Assert.Equal(Mnemonic.STI, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }
}

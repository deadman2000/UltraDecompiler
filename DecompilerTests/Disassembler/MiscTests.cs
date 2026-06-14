namespace DecompilerTests.Disassembler;

public class MiscTests : BaseTests
{
    [Fact]
    public void DisassembleInt()
    {
        var instructions = Disassemble("CD 21"); // INT 21h
        Assert.Equal(Mnemonic.INT, instructions[0].Mnemonic);
        Assert.Equal("21h", instructions[0].Operands);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand1.Type);
        Assert.Equal(0x21, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleEnter()
    {
        var instructions = Disassemble("C8 10 00 02"); // ENTER 0010h, 02
        Assert.Equal(Mnemonic.ENTER, instructions[0].Mnemonic);
        Assert.Equal("10h, 2", instructions[0].Operands);
        Assert.Equal(OperandType.Immediate16, instructions[0].Operand1.Type);
        Assert.Equal(0x0010, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand2.Type);
        Assert.Equal(2, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleNop()
    {
        var instructions = Disassemble("90"); // NOP
        Assert.Equal(Mnemonic.NOP, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleSimpleDisassembleOverload()
    {
        var disassembler = new X86Disassembler("B8 34 12".FromHex());
        disassembler.Disassemble(0);
        Assert.Single(disassembler.Instructions);
        Assert.Equal(Mnemonic.MOV, disassembler.Instructions[0].Mnemonic);
    }

    [Fact]
    public void DisassembleBranchTest()
    {
        var disassembler = new X86Disassembler("B8 34 12 C3".FromHex());
        var branch = disassembler.DisassembleBranch(0).ToList();
        Assert.Equal(2, branch.Count);
        Assert.Equal(Mnemonic.MOV, branch[0].Mnemonic);
        Assert.Equal(Mnemonic.RET, branch[1].Mnemonic);
    }
}

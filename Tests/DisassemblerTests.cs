using UltraDecompiler.Disassembler;

namespace Tests;

public class DisassemblerTests
{
    [Fact]
    public void DisassembleNearInderectCall()
    {
        var instructions = Disassemble("FF 16 46 00");
        Assert.Equal("CALL", instructions[0].Mnemonic);
        Assert.Equal("DS:0x0046", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleDirectNearJump()
    {
        var instructions = Disassemble("E9 05 00"); // JMP +5
        Assert.Equal("JMP", instructions[0].Mnemonic);
        Assert.Contains("0x0005", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleDirectShortJump()
    {
        var instructions = Disassemble("EB 05"); // JMP SHORT +5
        Assert.Equal("JMP", instructions[0].Mnemonic);
        Assert.Contains("0x0005", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleIndirectJumpWithBaseIndex()
    {
        var instructions = Disassemble("FF 27"); // JMP WORD PTR [BX]
        Assert.Equal("JMP", instructions[0].Mnemonic);
        Assert.Contains("BX", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleComplexIndirectCall()
    {
        // CALL WORD PTR [BX+SI+1234]
        var instructions = Disassemble("FF 90 34 12");
        Assert.Equal("CALL", instructions[0].Mnemonic);
        Assert.Contains("BX+SI", instructions[0].Operands);
        Assert.Contains("1234", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleFarIndirectJump()
    {
        // JMP DWORD PTR [1234] (far jump through memory)
        var instructions = Disassemble("FF 2E 34 12");
        Assert.Equal("JMP FAR", instructions[0].Mnemonic);
        Assert.Contains("1234", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleMovWithComplexMemory()
    {
        // MOV AX, [BP+DI+5]
        var instructions = Disassemble("8B 43 05");
        Assert.Equal("MOV", instructions[0].Mnemonic);
        Assert.Equal("AX, DS:BP+DI+5", instructions[0].Operands);
    }

    [Fact]
    public void DisassemblePushMemory()
    {
        // PUSH WORD PTR [1234]
        var instructions = Disassemble("FF 36 34 12");
        Assert.Equal("PUSH", instructions[0].Mnemonic);
        Assert.Contains("1234", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleImulThreeOperand()
    {
        // IMUL AX, [SI+4], 7
        var instructions = Disassemble("6B 44 04 07");
        Assert.Equal("IMUL", instructions[0].Mnemonic);
        Assert.Contains("AX", instructions[0].Operands);
        Assert.Contains("SI+4", instructions[0].Operands);
        Assert.Contains("7", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleWithSegmentOverride()
    {
        // ES: MOV AX, [1234]
        var instructions = Disassemble("26 8B 06 34 12");
        Assert.Equal("MOV", instructions[0].Mnemonic);
        Assert.Contains("ES:", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleLockPrefix()
    {
        // LOCK INC WORD PTR [BX]
        var instructions = Disassemble("F0 FF 07");
        Assert.Equal("LOCK INC", instructions[0].Mnemonic);
        Assert.Contains("BX", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleUnknownOpcode()
    {
        var instructions = Disassemble("0F 1F"); // 0F 1F = unknown
        Assert.StartsWith("DB", instructions[0].Mnemonic);
    }

    private static List<Instruction> Disassemble(string hex)
    {
        var disassembler = new X86Disassembler(hex.FromHex());
        disassembler.Disassemble(0);
        return disassembler.Instructions;
    }
}

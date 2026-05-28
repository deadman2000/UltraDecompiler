using UltraDecompiler.Disassembler;

namespace Tests.Disassembler;

/// <summary>
/// Тесты дизассемблера для инструкций переходов и вызовов (JMP, CALL, RET, LOOP, условные переходы).
/// Все методы перенесены полностью со всеми оригинальными проверками.
/// </summary>
public class JumpsCallsTests : BaseTests
{
    [Fact]
    public void DisassembleNearIndirectCall()
    {
        var instructions = Disassemble("FF 16 46 00"); // CALL [46h]
        Assert.Equal(Mnemonic.CALL, instructions[0].Mnemonic);
        Assert.Equal("[46h]", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(0x0046, instructions[0].Operand1.Value);
        Assert.Equal(AddressRegister.None, instructions[0].Operand1.BaseReg);
        Assert.Equal(AddressRegister.None, instructions[0].Operand1.IndexReg);
    }

    [Fact]
    public void DisassembleDirectNearJump()
    {
        var instructions = Disassemble("E9 05 00"); // JMP +5
        Assert.Equal(Mnemonic.JMP, instructions[0].Mnemonic);
        Assert.Equal("8", instructions[0].Operands);
        Assert.Equal(OperandType.Relative16, instructions[0].Operand1.Type);
        Assert.Equal(8, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleDirectShortJump()
    {
        var instructions = Disassemble("EB 05"); // JMP SHORT +5
        Assert.Equal(Mnemonic.JMP, instructions[0].Mnemonic);
        Assert.Equal("7", instructions[0].Operands);
        Assert.Equal(OperandType.Relative8, instructions[0].Operand1.Type);
        Assert.Equal(7, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleIndirectJumpWithBaseIndex()
    {
        var instructions = Disassemble("FF 27"); // JMP [BX]
        Assert.Equal(Mnemonic.JMP, instructions[0].Mnemonic);
        Assert.Contains("BX", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand1.BaseReg); // BX
    }

    [Fact]
    public void DisassembleComplexIndirectCall()
    {
        // CALL [BX+SI+1234h]
        var instructions = Disassemble("FF 90 34 12"); // CALL [BX+SI+1234h]
        Assert.Equal(Mnemonic.CALL, instructions[0].Mnemonic);
        Assert.Contains("BX+SI", instructions[0].Operands);
        Assert.Contains("1234", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(0x1234, instructions[0].Operand1.Value);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand1.BaseReg); // BX
        Assert.Equal(AddressRegister.SI, instructions[0].Operand1.IndexReg); // SI
    }

    [Fact]
    public void DisassembleFarIndirectJump()
    {
        // JMP FAR [1234h] (far jump through memory)
        var instructions = Disassemble("FF 2E 34 12"); // JMP FAR [1234h]
        Assert.Equal(Mnemonic.JMP_FAR, instructions[0].Mnemonic);
        Assert.Contains("1234", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(0x1234, instructions[0].Operand1.Value);
        Assert.Equal(AddressRegister.None, instructions[0].Operand1.BaseReg);
        Assert.Equal(AddressRegister.None, instructions[0].Operand1.IndexReg);
    }

    [Fact]
    public void DisassembleRetNear()
    {
        var instructions = Disassemble("C3"); // RET
        Assert.Equal(Mnemonic.RET, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleJeShort()
    {
        var instructions = Disassemble("74 05"); // JE +5
        Assert.Equal(Mnemonic.JE, instructions[0].Mnemonic);
        Assert.Equal("7", instructions[0].Operands);
        Assert.Equal(OperandType.Relative8, instructions[0].Operand1.Type);
        Assert.Equal(7, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleLoop()
    {
        var instructions = Disassemble("E2 05"); // LOOP +5
        Assert.Equal(Mnemonic.LOOP, instructions[0].Mnemonic);
        Assert.Equal("7", instructions[0].Operands);
        Assert.Equal(OperandType.Relative8, instructions[0].Operand1.Type);
        Assert.Equal(7, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleIret()
    {
        var instructions = Disassemble("CF"); // IRET
        Assert.Equal(Mnemonic.IRET, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleRetf()
    {
        var instructions = Disassemble("CB"); // RETF
        Assert.Equal(Mnemonic.RETF, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleJaShort()
    {
        var instructions = Disassemble("77 05"); // JA +5
        Assert.Equal(Mnemonic.JA, instructions[0].Mnemonic);
        Assert.Equal("7", instructions[0].Operands);
        Assert.Equal(OperandType.Relative8, instructions[0].Operand1.Type);
        Assert.Equal(7, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleJbShort()
    {
        var instructions = Disassemble("72 05"); // JB +5
        Assert.Equal(Mnemonic.JB, instructions[0].Mnemonic);
        Assert.Equal("7", instructions[0].Operands);
        Assert.Equal(OperandType.Relative8, instructions[0].Operand1.Type);
        Assert.Equal(7, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleJgShort()
    {
        var instructions = Disassemble("7F 05"); // JG +5
        Assert.Equal(Mnemonic.JG, instructions[0].Mnemonic);
        Assert.Equal("7", instructions[0].Operands);
        Assert.Equal(OperandType.Relative8, instructions[0].Operand1.Type);
        Assert.Equal(7, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleJleShort()
    {
        var instructions = Disassemble("7E 05"); // JLE +5
        Assert.Equal(Mnemonic.JLE, instructions[0].Mnemonic);
        Assert.Equal("7", instructions[0].Operands);
        Assert.Equal(OperandType.Relative8, instructions[0].Operand1.Type);
        Assert.Equal(7, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleCallNear()
    {
        var instructions = Disassemble("E8 05 00"); // CALL 8 (displacement +5 → target 8)
        Assert.Equal(Mnemonic.CALL, instructions[0].Mnemonic);
        Assert.Equal("8", instructions[0].Operands);
        Assert.Equal(OperandType.Relative16, instructions[0].Operand1.Type);
        Assert.Equal(8, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleRetfFar()
    {
        var instructions = Disassemble("CA"); // RETF_FAR (CA)
        Assert.Equal(Mnemonic.RETF_FAR, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleIndirectJumpMemoryTarget()
    {
        // JMP [0004] (косвенный, указатель в памяти = 0005h)
        var disassembler = new X86Disassembler("FF 26 04 00 05 00 C3".FromHex());
        disassembler.DataSegmentBase = 0;
        disassembler.Disassemble(0);
        Assert.Equal(Mnemonic.JMP, disassembler.Instructions[0].Mnemonic);
        // Covers the memory indirect jump target resolution in GetEffectiveJumpTarget (realAddr + read ushort)
    }
}

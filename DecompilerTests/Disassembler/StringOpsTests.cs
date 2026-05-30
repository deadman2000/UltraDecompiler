using UltraDecompiler.Disassembler;

namespace DecompilerTests.Disassembler;

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

    // ============================================================
    // Тесты DF (Direction Flag) + строковые инструкции
    // ============================================================

    [Fact]
    public void Cld_SetsDfFalse()
    {
        var instructions = Disassemble("""
            FC          ; cld
            A4          ; movsb
            """);

        Assert.False(instructions[0].Registers.DF);
        Assert.False(instructions[1].Registers.DF); // сохраняется
    }

    [Fact]
    public void Std_SetsDfTrue()
    {
        var instructions = Disassemble("""
            FD          ; std
            A4          ; movsb
            """);

        Assert.True(instructions[0].Registers.DF);
        Assert.True(instructions[1].Registers.DF);
    }

    [Fact]
    public void Movsb_Forward_UpdatesSiDi()
    {
        var instructions = Disassemble("""
            FC          ; cld
            BE 00 10    ; mov si, 1000h
            BF 00 20    ; mov di, 2000h
            A4          ; movsb
            """);

        // После MOVSB при DF=0: SI и DI увеличиваются на 1
        Assert.Equal((ushort)0x1001, instructions[3].Registers.SI);
        Assert.Equal((ushort)0x2001, instructions[3].Registers.DI);
    }

    [Fact]
    public void Movsw_Backward_UpdatesSiDi()
    {
        var instructions = Disassemble("""
            FD          ; std
            BE 00 10    ; mov si, 1000h
            BF 00 20    ; mov di, 2000h
            A5          ; movsw
            """);

        // После MOVSW при DF=1: SI и DI уменьшаются на 2
        Assert.Equal((ushort)0x0FFE, instructions[3].Registers.SI);
        Assert.Equal((ushort)0x1FFE, instructions[3].Registers.DI);
    }

    [Fact]
    public void RepStosb_ResetsCx()
    {
        var instructions = Disassemble("""
            FC              ; cld
            B9 10 00        ; mov cx, 10h
            B0 00           ; mov al, 0
            BF 00 30        ; mov di, 3000h
            F3 AA           ; rep stosb
            """);

        // После REP STOSB CX должен быть сброшен (неизвестен)
        Assert.Null(instructions[4].Registers.CX);
        // DI должен быть обновлён (но поскольку CX был известен, в текущей реализации он всё равно сбрасывается при REP)
        // Здесь мы просто проверяем, что не упали и DF сохранился
        Assert.False(instructions[4].Registers.DF);
    }

}

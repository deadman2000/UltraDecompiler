namespace Tests.Registers;

/// <summary>
/// Тесты регистров — арифметика, логика и унарные операции.
/// </summary>
public class ArithLogicTests : BaseTests
{
    [Fact]
    public void AddAlImm()
    {
        var instructions = Disassemble("""
            B0 05 ; mov al, 05h
            04 03 ; add al, 03h
            CD 21 ; int 21h
            """);
        Assert.Equal((byte)0x08, instructions[1].Registers.AL);
        Assert.Equal((byte)0x08, instructions[2].Registers.AL);
    }

    [Fact]
    public void SubAlImm()
    {
        var instructions = Disassemble("""
            B0 0A ; mov al, 0Ah
            2C 03 ; sub al, 03h
            CD 21 ; int 21h
            """);
        Assert.Equal((byte)0x07, instructions[1].Registers.AL);
    }

    [Fact]
    public void AndAlImm()
    {
        var instructions = Disassemble("""
            B0 FF ; mov al, 0FFh
            24 0F ; and al, 0Fh
            CD 21 ; int 21h
            """);
        Assert.Equal((byte)0x0F, instructions[1].Registers.AL);
    }

    [Fact]
    public void OrAlImm()
    {
        var instructions = Disassemble("""
            B0 10 ; mov al, 10h
            0C 01 ; or al, 01h
            CD 21 ; int 21h
            """);
        Assert.Equal((byte)0x11, instructions[1].Registers.AL);
    }

    [Fact]
    public void XorAlImm()
    {
        var instructions = Disassemble("""
            B0 FF ; mov al, 0FFh
            34 0F ; xor al, 0Fh
            CD 21 ; int 21h
            """);
        Assert.Equal((byte)0xF0, instructions[1].Registers.AL);
    }

    [Fact]
    public void AddBlImm()
    {
        var instructions = Disassemble("""
            B3 05    ; mov bl, 05h
            80 C3 03 ; add bl, 03h
            CD 21    ; int 21h
            """);
        Assert.Equal((byte)0x08, instructions[1].Registers.BL);
        Assert.Equal((byte)0x08, instructions[2].Registers.BL);
    }

    [Fact]
    public void SubChImm()
    {
        var instructions = Disassemble("""
            B5 0A    ; mov ch, 0Ah
            80 ED 03 ; sub ch, 03h
            CD 21    ; int 21h
            """);
        Assert.Equal((byte)0x07, instructions[1].Registers.CH);
    }

    [Fact]
    public void IncAl()
    {
        var instructions = Disassemble("""
            B0 05 ; mov al, 05h
            FE C0 ; inc al
            CD 21 ; int 21h
            """);
        Assert.Equal((byte)0x06, instructions[1].Registers.AL);
    }

    [Fact]
    public void DecAl()
    {
        var instructions = Disassemble("""
            B0 05 ; mov al, 05h
            FE C8 ; dec al
            CD 21 ; int 21h
            """);
        Assert.Equal((byte)0x04, instructions[1].Registers.AL);
    }

    [Fact]
    public void NotAl()
    {
        var instructions = Disassemble("""
            B0 05 ; mov al, 05h
            F6 D0 ; not al
            CD 21 ; int 21h
            """);
        Assert.Equal((byte)0xFA, instructions[1].Registers.AL); // ~0x05 = 0xFA
    }

    [Fact]
    public void NegAl()
    {
        var instructions = Disassemble("""
            B0 05 ; mov al, 05h
            F6 D8 ; neg al
            CD 21 ; int 21h
            """);
        Assert.Equal((byte)0xFB, instructions[1].Registers.AL); // -5 = 0xFB in 8bit
    }

    [Fact]
    public void XchgAlBl()
    {
        var instructions = Disassemble("""
            B0 05 ; mov al, 05h
            B3 0A ; mov bl, 0Ah
            86 C3 ; xchg al, bl
            CD 21 ; int 21h
            """);
        Assert.Equal((byte)0x0A, instructions[2].Registers.AL);
        Assert.Equal((byte)0x05, instructions[2].Registers.BL);
    }
}

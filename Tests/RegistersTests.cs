namespace Tests;

public class RegistersTests : BaseTests
{
    [Fact]
    public void MovAH()
    {
        var instructions = Disassemble("""
            B4 20;  mov ah, 20h
            B4 40;  mov ah, 40h
            CD 21;  int 21h
            """);
        Assert.Equal((byte)0x20, instructions[0].Registers.AH);
        Assert.Equal((byte)0x40, instructions[1].Registers.AH);
        Assert.Equal((byte)0x40, instructions[2].Registers.AH);
    }

    [Fact]
    public void MovAL()
    {
        var instructions = Disassemble("""
            B0 20;  mov ah, 20h
            B0 40;  mov ah, 40h
            CD 21;  int 21h
            """);
        Assert.Equal((byte)0x20, instructions[0].Registers.AL);
        Assert.Equal((byte)0x40, instructions[1].Registers.AL);
        Assert.Equal((byte)0x40, instructions[2].Registers.AL);
    }

    [Fact]
    public void MovAX()
    {
        var instructions = Disassemble("""
            B8 34 12; mov ax, 1234h
            B8 78 56; mov ax, 5678h
            CD 21; int 21h
            """);
        Assert.Equal((byte)0x34, instructions[0].Registers.AL);
        Assert.Equal((byte)0x12, instructions[0].Registers.AH);
        Assert.Equal((ushort)0x1234, instructions[0].Registers.AX);
        Assert.Equal((byte)0x78, instructions[1].Registers.AL);
        Assert.Equal((byte)0x56, instructions[1].Registers.AH);
        Assert.Equal((ushort)0x5678, instructions[1].Registers.AX);
        Assert.Equal((ushort)0x5678, instructions[2].Registers.AX);
    }

    [Fact]
    public void MovSPBPSIDI()
    {
        var instructions = Disassemble("""
            BC 34 12; mov sp, 1234h
            BD 78 56; mov bp, 5678h
            BE BC 9A; mov si, 9ABCh
            BF F0 DE; mov di, 0DEF0h
            CD 21; int 21h
            """);
        Assert.Equal((ushort)0x1234, instructions[0].Registers.SP);
        Assert.Equal((ushort)0x5678, instructions[1].Registers.BP);
        Assert.Equal((ushort)0x9ABC, instructions[2].Registers.SI);
        Assert.Equal((ushort)0xDEF0, instructions[3].Registers.DI);
        Assert.Equal((ushort)0xDEF0, instructions[4].Registers.DI);
    }

    [Fact]
    public void MovBL()
    {
        var instructions = Disassemble("""
            B3 30; mov bl, 30h
            B3 50; mov bl, 50h
            CD 21; int 21h
            """);
        Assert.Equal((byte)0x30, instructions[0].Registers.BL);
        Assert.Equal((byte)0x50, instructions[1].Registers.BL);
        Assert.Equal((byte)0x50, instructions[2].Registers.BL);
    }

    [Fact]
    public void MovCH()
    {
        var instructions = Disassemble("""
            B5 25; mov ch, 25h
            B5 45; mov ch, 45h
            CD 21; int 21h
            """);
        Assert.Equal((byte)0x25, instructions[0].Registers.CH);
        Assert.Equal((byte)0x45, instructions[1].Registers.CH);
        Assert.Equal((byte)0x45, instructions[2].Registers.CH);
    }

    [Fact]
    public void MovCX()
    {
        var instructions = Disassemble("""
            B9 34 12; mov cx, 1234h
            B9 78 56; mov cx, 5678h
            CD 21; int 21h
            """);
        Assert.Equal((byte)0x34, instructions[0].Registers.CL);
        Assert.Equal((byte)0x12, instructions[0].Registers.CH);
        Assert.Equal((byte)0x78, instructions[1].Registers.CL);
        Assert.Equal((byte)0x56, instructions[1].Registers.CH);
        Assert.Equal((byte)0x78, instructions[2].Registers.CL);
        Assert.Equal((byte)0x56, instructions[2].Registers.CH);

        // Проверки вычисляемых свойств
        Assert.Equal((ushort)0x1234, instructions[0].Registers.CX);
        Assert.Equal((ushort)0x5678, instructions[1].Registers.CX);
        Assert.Equal((ushort)0x5678, instructions[2].Registers.CX);
    }

    [Fact]
    public void MovBX()
    {
        var instructions = Disassemble("""
            BB 22 11; mov bx, 1122h
            BB 44 33; mov bx, 3344h
            CD 21; int 21h
            """);
        Assert.Equal((byte)0x22, instructions[0].Registers.BL);
        Assert.Equal((byte)0x11, instructions[0].Registers.BH);
        Assert.Equal((byte)0x44, instructions[1].Registers.BL);
        Assert.Equal((byte)0x33, instructions[1].Registers.BH);

        // Проверки вычисляемых свойств
        Assert.Equal((ushort)0x1122, instructions[0].Registers.BX);
        Assert.Equal((ushort)0x3344, instructions[1].Registers.BX);
    }
}
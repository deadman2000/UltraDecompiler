namespace DecompilerTests.Registers;

/// <summary>
/// Тесты регистров — перемещения значений (MOV в 8/16-битные и сегментные регистры).
/// </summary>
public class MoveTests : BaseTests
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
            B0 20;  mov al, 20h
            B0 40;  mov al, 40h
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
    public void MovRegToReg()
    {
        var instructions = Disassemble("""
            B0 20;  mov al, 20h
            88 C3;  mov bl, al      ; копируем известное значение
            B8 34 12; mov ax, 1234h
            89 C1;  mov cx, ax      ; копируем AX → CX
            89 E5;  mov bp, sp      ; копируем (sp ещё не известен)
            CD 21;  int 21h
            """);
        Assert.Equal((byte)0x20, instructions[1].Registers.BL);   // из AL
        Assert.Equal((ushort)0x1234, instructions[3].Registers.CX); // из AX
        // bp не должен измениться, т.к. sp неизвестен
        Assert.Null(instructions[4].Registers.BP);
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
    public void MovBH()
    {
        var instructions = Disassemble("""
            B7 30; mov bh, 30h
            B7 50; mov bh, 50h
            CD 21; int 21h
            """);
        Assert.Equal((byte)0x30, instructions[0].Registers.BH);
        Assert.Equal((byte)0x50, instructions[1].Registers.BH);
        Assert.Equal((byte)0x50, instructions[2].Registers.BH);
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
    public void MovCL()
    {
        var instructions = Disassemble("""
            B1 15; mov cl, 15h
            B1 25; mov cl, 25h
            CD 21; int 21h
            """);
        Assert.Equal((byte)0x15, instructions[0].Registers.CL);
        Assert.Equal((byte)0x25, instructions[1].Registers.CL);
        Assert.Equal((byte)0x25, instructions[2].Registers.CL);
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

    [Fact]
    public void MovDL()
    {
        var instructions = Disassemble("""
            B2 0A; mov dl, 0Ah
            B2 1A; mov dl, 1Ah
            CD 21; int 21h
            """);
        Assert.Equal((byte)0x0A, instructions[0].Registers.DL);
        Assert.Equal((byte)0x1A, instructions[1].Registers.DL);
        Assert.Equal((byte)0x1A, instructions[2].Registers.DL);
    }

    [Fact]
    public void MovDH()
    {
        var instructions = Disassemble("""
            B6 55; mov dh, 55h
            B6 66; mov dh, 66h
            CD 21; int 21h
            """);
        Assert.Equal((byte)0x55, instructions[0].Registers.DH);
        Assert.Equal((byte)0x66, instructions[1].Registers.DH);
        Assert.Equal((byte)0x66, instructions[2].Registers.DH);
    }

    [Fact]
    public void MovSegmentRegisters()
    {
        var instructions = Disassemble("""
            B8 00 10; mov ax, 1000h
            8E D8;    mov ds, ax
            8C DB;    mov bx, ds
            CD 21;    int 21h
            """);
        Assert.Equal((ushort)0x1000, instructions[1].Registers.DS);
        Assert.Equal((ushort)0x1000, instructions[2].Registers.BX);
        Assert.Equal((ushort)0x1000, instructions[3].Registers.BX);
    }
}

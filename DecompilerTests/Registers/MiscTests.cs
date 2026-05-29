namespace Tests.Registers;

/// <summary>
/// Тесты регистров — специальные инструкции (CBW, CWD, работа с сегментными регистрами).
/// </summary>
public class MiscTests : BaseTests
{
    [Fact]
    public void Cbw()
    {
        var instructions = Disassemble("""
            B0 80;  mov al, 80h
            98;     cbw
            CD 21;  int 21h
            """);
        Assert.Equal((byte)0xFF, instructions[1].Registers.AH);
        Assert.Equal((byte)0x80, instructions[1].Registers.AL);
    }

    [Fact]
    public void Cwd()
    {
        var instructions = Disassemble("""
            B8 00 80; mov ax, 8000h
            99;     cwd
            CD 21;  int 21h
            """);
        Assert.Equal((byte)0xFF, instructions[1].Registers.DH);
        Assert.Equal((byte)0xFF, instructions[1].Registers.DL);
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

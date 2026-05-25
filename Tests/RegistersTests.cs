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

    [Fact]
    public void AddAlImm()
    {
        var instructions = Disassemble("""
            B0 05;  mov al, 05h
            04 03;  add al, 03h
            CD 21;  int 21h
            """);
        Assert.Equal((byte)0x08, instructions[1].Registers.AL);
        Assert.Equal((byte)0x08, instructions[2].Registers.AL);
    }

    [Fact]
    public void SubAlImm()
    {
        var instructions = Disassemble("""
            B0 0A;  mov al, 0Ah
            2C 03;  sub al, 03h
            CD 21;  int 21h
            """);
        Assert.Equal((byte)0x07, instructions[1].Registers.AL);
    }

    [Fact]
    public void AndAlImm()
    {
        var instructions = Disassemble("""
            B0 FF;  mov al, 0FFh
            24 0F;  and al, 0Fh
            CD 21;  int 21h
            """);
        Assert.Equal((byte)0x0F, instructions[1].Registers.AL);
    }

    [Fact]
    public void OrAlImm()
    {
        var instructions = Disassemble("""
            B0 10;  mov al, 10h
            0C 01;  or al, 01h
            CD 21;  int 21h
            """);
        Assert.Equal((byte)0x11, instructions[1].Registers.AL);
    }

    [Fact]
    public void XorAlImm()
    {
        var instructions = Disassemble("""
            B0 FF;  mov al, 0FFh
            34 0F;  xor al, 0Fh
            CD 21;  int 21h
            """);
        Assert.Equal((byte)0xF0, instructions[1].Registers.AL);
    }

    [Fact]
    public void IncAl()
    {
        var instructions = Disassemble("""
            B0 05;  mov al, 05h
            FE C0;  inc al
            CD 21;  int 21h
            """);
        Assert.Equal((byte)0x06, instructions[1].Registers.AL);
    }

    [Fact]
    public void DecAl()
    {
        var instructions = Disassemble("""
            B0 05;  mov al, 05h
            FE C8;  dec al
            CD 21;  int 21h
            """);
        Assert.Equal((byte)0x04, instructions[1].Registers.AL);
    }

    [Fact]
    public void NotAl()
    {
        var instructions = Disassemble("""
            B0 05;  mov al, 05h
            F6 D0;  not al
            CD 21;  int 21h
            """);
        Assert.Equal((byte)0xFA, instructions[1].Registers.AL); // ~0x05 = 0xFA
    }

    [Fact]
    public void NegAl()
    {
        var instructions = Disassemble("""
            B0 05;  mov al, 05h
            F6 D8;  neg al
            CD 21;  int 21h
            """);
        Assert.Equal((byte)0xFB, instructions[1].Registers.AL); // -5 = 0xFB in 8bit
    }

    [Fact]
    public void XchgAlBl()
    {
        var instructions = Disassemble("""
            B0 05;  mov al, 05h
            B3 0A;  mov bl, 0Ah
            86 C3;  xchg al, bl
            CD 21;  int 21h
            """);
        Assert.Equal((byte)0x0A, instructions[2].Registers.AL);
        Assert.Equal((byte)0x05, instructions[2].Registers.BL);
    }

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

    [Fact]
    public void RegisterExpressions_8bit_SetGet_Logic()
    {
        // Тест новой логики 8-битных регистров в Decompilation.RegisterExpressions
        var regs = RegisterExpressions.InitZero();

        // Установка 16-bit AX
        var axExpr = new ConstExpr(0x1234);
        regs = regs.Set16(0, axExpr);
        Assert.Null(regs.AH);
        Assert.Null(regs.AL);
        Assert.Equal(axExpr, regs.Get16(0));

        // Установка AH - разбиваем X на AL = X & 0xff, X=null
        var ahExpr = new ConstExpr(0x12);
        regs = regs.Set8(4, ahExpr); // AH=4
        Assert.Equal(ahExpr, regs.AH);
        Assert.NotNull(regs.AL); // должна быть LowByte из прежнего X
        Assert.Null(regs.AX);
        Assert.Equal(ahExpr, regs.Get8(4));

        // Установка AL - оба установлены, Get16 = (AH<<8)|AL
        var alExpr = new ConstExpr(0x34);
        regs = regs.Set8(0, alExpr); // AL=0
        Assert.Equal(alExpr, regs.AL);
        var combined = regs.Get16(0);
        Assert.NotNull(combined);
        // проверка типа выражения
        Assert.Contains("<< 8", combined.ToString());
        Assert.Contains("| ", combined.ToString());

        // Установка обратно AX - H/L в null
        var newAx = new ConstExpr(0x5678);
        regs = regs.Set16(0, newAx);
        Assert.Null(regs.AH);
        Assert.Null(regs.AL);
        Assert.Equal(newAx, regs.Get16(0));
    }
}
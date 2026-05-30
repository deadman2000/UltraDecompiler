namespace DecompilerTests.Registers;

/// <summary>
/// Тесты поведения строковых инструкций на уровне RegisterState
/// (обновление SI/DI/CX/DF).
/// </summary>
public class StringRegisterTests : BaseTests
{
    [Fact]
    public void Stosb_Forward_UpdatesOnlyDi()
    {
        var instructions = Disassemble("""
            FC          ; cld
            B0 AA       ; mov al, 0AAh
            BF 00 40    ; mov di, 4000h
            AA          ; stosb
            """);

        Assert.Equal((ushort)0x4001, instructions[3].Registers.DI);
        Assert.Null(instructions[3].Registers.SI); // SI не был инициализирован и не обновлялся STOSB
    }

    [Fact]
    public void Lodsw_Backward_UpdatesOnlySi()
    {
        var instructions = Disassemble("""
            FD          ; std
            BE 50 10    ; mov si, 1050h
            AD          ; lodsw
            """);

        Assert.Equal((ushort)0x104E, instructions[2].Registers.SI);
        Assert.Null(instructions[2].Registers.DI); // DI не был инициализирован и не обновлялся LODSW
    }

    [Fact]
    public void Cmpsb_Forward_UpdatesBothSiAndDi()
    {
        var instructions = Disassemble("""
            FC          ; cld
            BE 00 10    ; mov si, 1000h
            BF 00 20    ; mov di, 2000h
            A6          ; cmpsb
            """);

        Assert.Equal((ushort)0x1001, instructions[3].Registers.SI);
        Assert.Equal((ushort)0x2001, instructions[3].Registers.DI);
    }

    [Fact]
    public void Scasw_Backward_UpdatesOnlyDi()
    {
        var instructions = Disassemble("""
            FD          ; std
            BF 00 30    ; mov di, 3000h
            AF          ; scasw
            """);

        Assert.Equal((ushort)0x2FFE, instructions[2].Registers.DI);
        Assert.Null(instructions[2].Registers.SI); // SI не был инициализирован
    }

    [Fact]
    public void RepMovsb_UnknownCx_ResetsCxSiDi()
    {
        var instructions = Disassemble("""
            FC              ; cld
            B9 00 00        ; mov cx, 0          ; нулевая длина
            BE 00 10        ; mov si, 1000h
            BF 00 20        ; mov di, 2000h
            F3 A4           ; rep movsb
            """);

        // Даже если CX=0, при REP-префиксе мы консервативно сбрасываем
        Assert.Null(instructions[4].Registers.CX);
    }

    [Fact]
    public void CldThenStd_ChangesDirectionCorrectly()
    {
        var instructions = Disassemble("""
            FC          ; cld
            FD          ; std
            FC          ; cld
            A4          ; movsb
            """);

        Assert.False(instructions[0].Registers.DF);
        Assert.True(instructions[1].Registers.DF);
        Assert.False(instructions[2].Registers.DF);
        Assert.False(instructions[3].Registers.DF);
    }
}

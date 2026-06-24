namespace DecompilerTests.Decompilation;

using UltraDecompiler.Decompilation.Heuristics;

/// <summary>Детектор пустых тел процедур (<c>nullsub</c>).</summary>
public sealed class EmptyProcedureDetectorTests : BaseTests
{
    // /Ox func.c / foo: одна RET
    [Fact]
    public void IsEmptyBody_SingleRet_ReturnsTrue()
    {
        var instructions = Disassemble("C3");

        Assert.True(EmptyProcedureDetector.IsEmptyBody(instructions));
    }

    // /Od func.c / foo: пролог, неиспользуемый локал, эпилог — не считается пустым телом
    [Fact]
    public void IsEmptyBody_PrologueLeaveRet_ReturnsFalse()
    {
        var instructions = Disassemble("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            83 EC 02  ; SUB SP, 2
            C9        ; LEAVE
            C3        ; RET
            """);

        Assert.False(EmptyProcedureDetector.IsEmptyBody(instructions));
    }

    // main с вызовом и return 0 — не пустое тело
    [Fact]
    public void IsEmptyBody_CallAndReturnValue_ReturnsFalse()
    {
        var instructions = Disassemble("""
            E8 FB FF  ; CALL foo
            B8 00 00  ; MOV AX, 0
            C3        ; RET
            """);

        Assert.False(EmptyProcedureDetector.IsEmptyBody(instructions));
    }
}

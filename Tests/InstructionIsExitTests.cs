namespace Tests;

public class InstructionIsExitTests : BaseTests
{
    [Fact]
    public void IsExit_Int21_AH_4C()
    {
        var instructions = Disassemble("""
            B4 4C;  mov ah, 4ch
            CD 21;  int 21h
            """);
        Assert.True(instructions[1].IsExit);
    }

    [Fact]
    public void IsExit_Int21_AX_4CFF()
    {
        var instructions = Disassemble("""
            B8 FF 4C;  mov ax, 4cffh
            CD 21;  int 21h
            """);
        Assert.True(instructions[1].IsExit);
    }

    [Fact]
    public void IsNotExit_Int21_AH_30()
    {
        var instructions = Disassemble("""
            B4 30;  mov ah, 30h
            CD 21;  int 21h
            """);
        Assert.False(instructions[1].IsExit);
    }
}

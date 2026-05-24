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
}

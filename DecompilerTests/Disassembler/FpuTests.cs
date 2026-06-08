using UltraDecompiler.Disassembler;

namespace DecompilerTests.Disassembler;

public class FpuTests : BaseTests
{
    [Fact]
    public void DisassembleFwait()
    {
        var instructions = Disassemble("9B"); // FWAIT
        Assert.Equal(Mnemonic.FWAIT, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleFpu_D9E1_Fld1()
    {
        // Типичный thunk QuickC: 9B D9 E1 90 C3 (FWAIT; FLD1; NOP; RET)
        var instructions = Disassemble("9B D9 E1 90 C3");
        Assert.Equal(Mnemonic.FWAIT, instructions[0].Mnemonic);
        Assert.Equal(Mnemonic.FPU, instructions[1].Mnemonic);
        Assert.Contains("fld1", instructions[1].Commentary ?? "");
        Assert.Equal(Mnemonic.NOP, instructions[2].Mnemonic);
        Assert.Equal(Mnemonic.RET, instructions[3].Mnemonic);
    }

    [Fact]
    public void DisassembleFpu_DeC1_Faddp()
    {
        // FADDP ST(1), ST
        var instructions = Disassemble("DE C1");
        Assert.Equal(Mnemonic.FPU, instructions[0].Mnemonic);
        Assert.Contains("faddp", instructions[0].Commentary ?? "");
    }
}

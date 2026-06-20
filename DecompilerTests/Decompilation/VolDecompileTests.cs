using TestSupport;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция <c>vol.c</c>: far-указатель на видеопамять (цикл — задача CFG structurer).</summary>
public sealed class VolDecompileTests
{
    // Исходник QuickC/PROGRAMS/vol.c:
    //   char far *vid = (char far *)0xB8000000L;
    //   for (i = 0; i < 80; i++) vid[i * 2] = 'X';
    //   printf("done\n");
    [Fact(Skip = "NotImplemented")]
    public void Decompile_Vol_EmitsFarPointerLiteral()
    {
        var result = DecompileTestHelper.DecompileExample("vol.c");

        Assert.True(result.Success);

        var source = DecompileTestHelper.ReadPrimarySource(result);
        Assert.Contains("char far *", source);
        Assert.Contains("(char far *)0xB8000000L", source);
        Assert.Contains("'X'", source);
        Assert.Contains("printf(\"done\\n\")", source);
    }
}

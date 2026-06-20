using TestSupport;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция <c>fptr.c</c>: запись в видеопамять через <c>char far *</c>.</summary>
public sealed class FptrDecompileTests
{
    // Исходник QuickC/PROGRAMS/fptr.c:
    //   char far *screen = (char far *)0xB8000000L;
    //   *screen = 'A';
    //   printf("ok\n");
    // Ожидаемый фрагмент main.c:
    //   char far *varN = (char far *)0xB8000000L;
    //   *varN = 'A';
    //   printf("ok\n");
    [Fact(Skip = "NotImplemented")]
    public void Decompile_Fptr_EmitsFarPointer()
    {
        var result = DecompileTestHelper.DecompileExample("fptr.c");

        Assert.True(result.Success);

        var source = DecompileTestHelper.ReadPrimarySource(result);
        Assert.Contains("char far *", source);
        Assert.Contains("(char far *)0xB8000000L", source);
        Assert.Contains("*var", source);
        Assert.Contains("= 'A'", source);
        Assert.Contains("printf(\"ok\\n\")", source);
        Assert.DoesNotContain("varSS:[", source);
        Assert.DoesNotContain(":var", source);
    }
}

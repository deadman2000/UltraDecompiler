using TestSupport;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция <c>enab.c</c>: <c>_disable</c> / <c>_enable</c> из CLI/STI.</summary>
public sealed class EnabDecompileTests
{
    // Исходник enab.c: _disable(); printf(...); _enable(); printf(...);
    // Ожидаемый фрагмент main.c:
    //   #include <DOS.H>
    //   _disable();
    //   printf("off\n");
    //   _enable();
    [Fact(Skip = "NotImplemented")]
    public void Decompile_Enab_EmitsEnableDisable()
    {
        var result = DecompileTestHelper.DecompileExample("enab.c");

        Assert.True(result.Success);
        var mainSource = DecompileTestHelper.ReadGeneratedFile(
            result,
            static fileName => fileName.EndsWith("main.c", StringComparison.Ordinal));

        Assert.Contains("#include <DOS.H>", mainSource);
        Assert.Contains("_disable();", mainSource);
        Assert.Contains("_enable();", mainSource);
        Assert.DoesNotContain("_disable(0", mainSource);
        Assert.DoesNotContain("_enable(618", mainSource);
        Assert.Contains("printf(\"off\\n\")", mainSource);
        Assert.Contains("printf(\"on\\n\")", mainSource);
        Assert.DoesNotContain("sub_", mainSource);
    }
}

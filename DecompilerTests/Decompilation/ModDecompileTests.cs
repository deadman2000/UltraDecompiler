using TestSupport;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция <c>mod.c</c>: знаковые <c>%</c> и <c>/</c> для <c>int</c>.</summary>
public sealed class ModDecompileTests
{
    // Исходник QuickC/PROGRAMS/mod.c:
    //   int a = 17, b = 5;
    //   printf("%d %d\n", a % b, a / b);
    // Ожидаемый фрагмент main.c:
    //   var1 = 17; var2 = 5;
    //   printf("%d %d\n", var1 % var2, var1 / var2);
    [Fact(Skip = "NotImplemented")]
    public void Decompile_Mod_EmitsModulo()
    {
        var result = DecompileTestHelper.DecompileExample("mod.c");

        Assert.True(result.Success);
        var mainSource = File.ReadAllText(
            result.OutputFiles.First(p => p.EndsWith("main.c", StringComparison.Ordinal)));

        Assert.Contains("var1 = 17", mainSource);
        Assert.Contains("var2 = 5", mainSource);
        Assert.Contains("printf(\"%d %d\\n\", var1 % var2, var1 / var2)", mainSource);
        Assert.DoesNotContain("temp1", mainSource);
        Assert.DoesNotContain("__aNl", mainSource);
        Assert.DoesNotContain("32768", mainSource);
    }
}

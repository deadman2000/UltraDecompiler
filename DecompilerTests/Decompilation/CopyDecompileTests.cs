using TestSupport;

namespace DecompilerTests.Decompilation;

/// <summary>Интеграционные тесты декомпиляции <c>copy.c</c> (локали, строковые литералы).</summary>
public sealed class CopyDecompileTests
{
    // Исходник copy.c: char buf[20], int a=10, char buf2[30], int b=8; copy/copy2; printf("%s\n", buf).
    // Ожидаемый фрагмент main.c:
    //   char var1[20]; int var2; char var3[30]; int var4;
    //   var2 = 10; var4 = 8;
    //   sub_0010(var1, "test"); sub_0010(var3, "test3"); sub_0048(var3, "test3");
    [Fact(Skip = "NotImplemented")]
    public void Decompile_Copy_EmitsCharArrayLocal()
    {
        var result = DecompileTestHelper.DecompileExample(sourceFileName: "copy.c");

        Assert.True(result.Success);

        var mainSource = DecompileTestHelper.ReadPrimarySource(result);
        Assert.Contains("char var1[20];", mainSource);
        Assert.Contains("int var2;", mainSource);
        Assert.Contains("char var3[30];", mainSource);
        Assert.Contains("int var4;", mainSource);
        Assert.DoesNotContain("char var2[", mainSource);
        Assert.DoesNotContain("char var4[", mainSource);
        Assert.Contains("var2 = 10;", mainSource);
        Assert.Contains("var4 = 8;", mainSource);
        Assert.Contains("sub_0010(var1, \"test\")", mainSource);
        Assert.Contains("sub_0010(var3, \"test3\")", mainSource);
        Assert.Contains("sub_0048(var3, \"test3\")", mainSource);
        Assert.Contains("printf(\"%s\\n\", var1)", mainSource);
    }
}

using TestSupport;
using UltraDecompiler.Common;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция <c>func.c</c> с <c>/Ox</c>: пустая <c>foo()</c> → <c>nullsub_N</c>.</summary>
public sealed class FuncOxDecompileTests
{
    // Исходник QuickC/PROGRAMS/func.c (/Ox):
    //   void foo() { int a; return; }
    //   int main(void) { foo(); return 0; }
    // foo() оптимизируется до одной RET; не должна сопоставляться с .LIB.
    [Fact]
    public void Decompile_FuncOx_EmptyFoo_IsNullSub()
    {
        var result = DecompileTestHelper.DecompileExample(
            sourceFileName: "func.c",
            optimization: OptimizationLevel.EnabledFull);

        Assert.True(result.Success);
        var source = DecompileTestHelper.ReadPrimarySource(result);

        Assert.Contains("void nullsub_1(void)", source);
        Assert.Contains("nullsub_1();", source);
        Assert.DoesNotContain("sub_0010", source);
    }
}

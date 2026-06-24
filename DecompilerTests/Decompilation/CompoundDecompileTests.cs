using TestSupport;
using UltraDecompiler.Common;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция <c>compound.c</c>: различение a=a±K и a±=K, a=a±b и a±=b.</summary>
public sealed class CompoundDecompileTests
{
    // QuickC/PROGRAMS/compound.c — все варианты составного присваивания.
    [Theory(Skip = "NotImplemented")]
    [InlineData(OptimizationLevel.Disabled)]
    [InlineData(OptimizationLevel.EnabledFull)]
    public void Decompile_Compound_EmitsCompoundAssignment(OptimizationLevel optimization)
    {
        var result = DecompileTestHelper.DecompileExample("compound.c", optimization: optimization);

        Assert.True(result.Success);
        var mainSource = DecompileTestHelper.ReadGeneratedFile(
            result,
            static fileName => fileName.EndsWith("main.c", StringComparison.Ordinal));

        Assert.Contains("var1 = var1 + 5", mainSource);
        Assert.Contains("var1 += 5", mainSource);
        Assert.Contains("var1 = var1 - 5", mainSource);
        Assert.Contains("var1 -= 5", mainSource);
        Assert.Contains("var1 = var1 + var2", mainSource);
        Assert.Contains("var1 += var2", mainSource);
        Assert.Contains("var1 = var1 - var2", mainSource);
        Assert.Contains("var1 -= var2", mainSource);
        Assert.DoesNotContain("65535", mainSource);
        Assert.DoesNotContain("65531", mainSource);
        Assert.DoesNotContain("temp1", mainSource);
    }
}
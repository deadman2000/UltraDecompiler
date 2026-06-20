using TestSupport;
using UltraDecompiler.Compilation;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция <c>incdec.c</c>: различение a=a±1, a++/a-- и выражений ++a/a++.</summary>
public sealed class IncDecDecompileTests
{
    // Исходник QuickC/PROGRAMS/incdec.c (все варианты инкремента/декремента).
    // Ожидаемый фрагмент main.c:
    //   var1 = var1 + 1; var1++; var1++;
    //   var2 = ++var1; var2 = var1++;
    //   var1 = var1 - 1; var1--; var1--;
    [Theory(Skip = "NotImplemented")]
    [InlineData(OptimizationLevel.Disabled)]
    [InlineData(OptimizationLevel.EnabledFull)]
    public void Decompile_Incdec_EmitsAssemblyFaithfulStatements(OptimizationLevel optimization)
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new Decompiler().Decompile(
                ExeProvider.Get("incdec.c", optimization: optimization),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);
            var mainSource = File.ReadAllText(
                result.OutputFiles.First(p => p.EndsWith("main.c", StringComparison.Ordinal)));

            Assert.Contains("var1 = 10", mainSource);
            Assert.Contains("var1 = var1 + 1", mainSource);
            Assert.Contains("var1++", mainSource);
            Assert.Contains("var2 = ++var1", mainSource);
            Assert.Contains("var2 = var1++", mainSource);
            Assert.Contains("var1 = var1 - 1", mainSource);
            Assert.Contains("var1--", mainSource);
            Assert.DoesNotContain("65535", mainSource);
            Assert.DoesNotContain("temp1", mainSource);
            Assert.DoesNotContain("var1 += 1", mainSource);
            Assert.DoesNotContain("var1 -= 1", mainSource);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }
}

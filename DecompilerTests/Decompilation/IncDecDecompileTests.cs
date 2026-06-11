using TestSupport;
using UltraDecompiler.Decompilation;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция <c>incdec.c</c>: префиксный/суффиксный inc/dec и явные add/sub.</summary>
public sealed class IncDecDecompileTests
{
    // Исходник QuickC/PROGRAMS/incdec.c:
    //   a = 10; a = a + 1; a++; a = a - 1; a--;
    // Ожидаемый фрагмент main.c (без свёртки в 65535 и без temp-локалей):
    //   var1 = 10;
    //   var1 = var1 + 1;
    //   var1++;
    //   var1 = var1 - 1;
    //   var1--;
    [Fact]
    public void Decompile_Incdec_EmitsAssemblyFaithfulStatements()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new Decompiler().Decompile(
                ExeProvider.Get("incdec.c"),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);
            var mainSource = File.ReadAllText(
                result.OutputFiles.First(p => p.EndsWith("main.c", StringComparison.Ordinal)));

            Assert.Contains("var1 = 10", mainSource);
            Assert.Contains("var1 = var1 + 1", mainSource);
            Assert.Contains("var1++", mainSource);
            Assert.Contains("var1 = var1 - 1", mainSource);
            Assert.Contains("var1--", mainSource);
            Assert.DoesNotContain("65535", mainSource);
            Assert.DoesNotContain("temp1", mainSource);
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

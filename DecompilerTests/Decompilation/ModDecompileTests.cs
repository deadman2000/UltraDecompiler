using TestSupport;
using UltraDecompiler.Decompilation;

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
    [Fact]
    public void Decompile_Mod_EmitsPercentAndSlashWithoutTemps()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new Decompiler().Decompile(
                ExeProvider.Get("mod.c"),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

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
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }
}

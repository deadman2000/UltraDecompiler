using TestSupport;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция <c>glob.c</c>: глобальная переменная и отдельная функция <c>bump</c>.</summary>
public sealed class GlobDecompileTests
{
    // Исходник QuickC/PROGRAMS/glob.c:
    //   int counter = 0; void bump(void) { counter++; }
    //   main: bump(); bump(); printf("%d\n", counter);
    // Ожидаемый фрагмент GLOB.c:
    //   int global1 = 0;
    //   void sub_0010(void) { global1++; }
    //   printf("%d\n", global1);
    [Fact]
    public void Decompile_Glob_EmitsGlobalVariableAccess()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new Decompiler().Decompile(
                ExeProvider.Get("glob.c"),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);

            var source = DecompileTestHelper.ReadPrimarySource(result);
            Assert.Contains("int global1 = 0;", source);
            Assert.Contains("global1++", source);
            Assert.Contains("printf(\"%d\\n\", global1)", source);
            Assert.DoesNotContain("_psp:[", source);
            Assert.DoesNotContain("Psp.", source);
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

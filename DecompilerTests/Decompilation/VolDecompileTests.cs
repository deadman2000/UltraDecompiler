using TestSupport;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция <c>vol.c</c>: цикл записи в видеопамять через <c>char far *</c>.</summary>
public sealed class VolDecompileTests
{
    // Исходник QuickC/PROGRAMS/vol.c:
    //   char far *vid = (char far *)0xB8000000L;
    //   for (i = 0; i < 80; i++) vid[i * 2] = 'X';
    //   printf("done\n");
    // Ожидаемый фрагмент main.c:
    //   char far *varN = (char far *)0xB8000000L;
    //   for (...; ... < 80; ...) { varN[... << 1] = 'X'; }
    [Fact]
    public void Decompile_Vol_EmitsFarPointerIndexedLoop()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new Decompiler().Decompile(
                ExeProvider.Get("vol.c"),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);

            var source = DecompileTestHelper.ReadPrimarySource(result);
            Assert.Contains("char far *", source);
            Assert.Contains("(char far *)0xB8000000L", source);
            Assert.Contains("for (", source);
            Assert.Contains("< 80", source);
            Assert.Contains("<< 1", source);
            Assert.Contains("'X'", source);
            Assert.Contains("printf(\"done\\n\")", source);
            Assert.DoesNotContain("temp", source);
            Assert.DoesNotContain("varSS:[", source);
            Assert.DoesNotContain(":var", source);
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

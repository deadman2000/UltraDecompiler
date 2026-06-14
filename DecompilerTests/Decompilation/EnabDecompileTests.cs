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
    [Fact]
    public void Decompile_Enab_EmitsDisableEnableFromCliSti()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new Decompiler().Decompile(
                ExeProvider.Get("enab.c"),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);
            var mainSource = File.ReadAllText(
                result.OutputFiles.First(path => path.EndsWith("main.c", StringComparison.Ordinal)));

            Assert.Contains("#include <DOS.H>", mainSource);
            Assert.Contains("_disable();", mainSource);
            Assert.Contains("_enable();", mainSource);
            Assert.DoesNotContain("_disable(0", mainSource);
            Assert.DoesNotContain("_enable(618", mainSource);
            Assert.Contains("printf(\"off\\n\")", mainSource);
            Assert.Contains("printf(\"on\\n\")", mainSource);
            Assert.DoesNotContain("sub_", mainSource);
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

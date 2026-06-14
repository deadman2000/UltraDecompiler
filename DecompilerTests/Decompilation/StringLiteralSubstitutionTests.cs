using TestSupport;

namespace DecompilerTests.Decompilation;

/// <summary>
/// Проверка восстановления строковых литералов (char*) для near-указателей в DGROUP.
/// </summary>
public class StringLiteralSubstitutionTests
{
    // hello.c → main.c: printf("Hello world\n"), а не printf(618) / printf(0x26A)
    [Fact]
    public void Decompile_HelloGs_MaterializesPrintfFormatString()
    {
        var mainSource = DecompileMainSource(ExeProvider.Get("hello.c"));

        Assert.Contains("printf(\"Hello world\\n\")", mainSource);
        Assert.DoesNotContain("printf(618", mainSource);
        Assert.DoesNotContain("printf(0x", mainSource, StringComparison.OrdinalIgnoreCase);
    }

    // add.c → main.c: printf("%d", ...), форматная строка — литерал, не near-адрес DGROUP
    [Fact]
    public void Decompile_AddGs_MaterializesPrintfFormatString()
    {
        var mainSource = DecompileMainSource(ExeProvider.Get("add.c"));

        Assert.Contains("printf(\"%d\",", mainSource);
        Assert.DoesNotContain("printf(618", mainSource);
        Assert.DoesNotContain("printf(0x", mainSource, StringComparison.OrdinalIgnoreCase);
    }

    private static string DecompileMainSource(string exePath)
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var decompiler = new Decompiler();
            var result = decompiler.Decompile(
                exePath,
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);

            var mainPath = DecompileTestHelper.GetPrimarySourcePath(result);
            return File.ReadAllText(mainPath);
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

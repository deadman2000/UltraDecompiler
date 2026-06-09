using UltraDecompiler.Decompilation;

namespace DecompilerTests.Decompilation;

/// <summary>
/// Проверка восстановления строковых литералов (char*) для near-указателей в DGROUP.
/// </summary>
public class StringLiteralSubstitutionTests
{
    [Fact]
    public void Decompile_HelloGs_MaterializesPrintfFormatString()
    {
        var mainSource = DecompileMainSource("HELLO_GS.EXE");

        Assert.Contains("printf(\"Hello world\\n\")", mainSource);
        Assert.DoesNotContain("printf(618", mainSource);
        Assert.DoesNotContain("printf(0x", mainSource, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decompile_AddGs_MaterializesPrintfFormatString()
    {
        var mainSource = DecompileMainSource("ADD_GS.EXE");

        Assert.Contains("printf(\"%d\",", mainSource);
        Assert.DoesNotContain("printf(618", mainSource);
        Assert.DoesNotContain("printf(0x", mainSource, StringComparison.OrdinalIgnoreCase);
    }

    private static string DecompileMainSource(string exeName)
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var decompiler = new Decompiler();
            var result = decompiler.Decompile(
                QuickCTestAssets.ProgramsPathOf(exeName),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);

            var mainPath = result.OutputFiles.First(path => path.EndsWith("main.c", StringComparison.Ordinal));
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

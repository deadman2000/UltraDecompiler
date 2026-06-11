using TestSupport;
using UltraDecompiler.Decompilation;

namespace DecompilerTests.Decompilation;

public class StrcpDecompileTests
{
    [Fact]
    public void Decompile_StrcpGs_StrcpyFirstArgIsCharArray()
    {
        var mainSource = DecompileMainSource(ExeProvider.Get("strcp.c"));

        Assert.Contains("char var", mainSource);
        Assert.Contains("strcpy(var", mainSource);
        Assert.DoesNotContain("struct find_t", mainSource);
        Assert.DoesNotContain("&var", mainSource);
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

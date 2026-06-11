using TestSupport;
using UltraDecompiler.Decompilation;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция примеров со структурами из заголовков QuickC (<c>dos.h</c> и т.п.).</summary>
public class DosStructDecompileTests
{
    [Fact]
    public void Decompile_DosGs_DeclaresDosdateStructAndFieldAccess()
    {
        AssertDosdateMain(DecompileMainSource(ExeProvider.Get("dos.c")));
    }

    [Fact]
    public void Decompile_DosWithExtraLocals_DeclaresDosdateStructAndFieldAccess()
    {
        var mainSource = DecompileMainSource(ExeProvider.Get("dvars.c"));
        AssertDosdateMain(mainSource);
        Assert.Matches(@"int var\d+;", mainSource);
        Assert.True(CountOccurrences(mainSource, "int var") >= 2, "Ожидаются дополнительные int-локали до и после struct.");
    }

    private static void AssertDosdateMain(string mainSource)
    {
        Assert.Contains("struct dosdate_t", mainSource);
        Assert.Contains("_dos_getdate(&", mainSource);
        Assert.Contains(".month", mainSource);
        Assert.Contains(".day", mainSource);
        Assert.Contains(".year", mainSource);
        Assert.DoesNotContain("temp1", mainSource);
        Assert.DoesNotContain("varSS", mainSource);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
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

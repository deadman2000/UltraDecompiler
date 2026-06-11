using TestSupport;
using UltraDecompiler.Decompilation;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция <c>strcp.c</c>: локальный буфер и <c>strcpy</c> без ложного struct-типа.</summary>
public class StrcpDecompileTests
{
    // Исходник QuickC/PROGRAMS/strcp.c:
    //   char buf[16];
    //   strcpy(buf, "hello");
    //   printf("%s\n", buf);
    // Ожидаемый фрагмент main.c:
    //   char var1[16];
    //   strcpy(var1, "hello");
    //   printf("%s\n", var1);
    // Первый аргумент strcpy — массив char, а не struct find_t* и не &var1.
    [Fact]
    public void Decompile_StrcpGs_StrcpyFirstArgIsCharArray()
    {
        var mainSource = DecompileMainSource(ExeProvider.Get("strcp.c"));

        Assert.Contains("char var", mainSource);
        Assert.Contains("strcpy(var", mainSource);
        // Буфер не должен ошибочно сопоставляться со struct find_t из dir.h
        Assert.DoesNotContain("struct find_t", mainSource);
        // strcpy принимает массив напрямую, без лишнего взятия адреса
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

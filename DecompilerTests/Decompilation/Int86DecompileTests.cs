using TestSupport;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция QuickC/PROGRAMS/int86.c — int86 и union REGS на стеке.</summary>
public class Int86DecompileTests
{
    // Исходник QuickC/PROGRAMS/int86.c:
    //   union REGS in, out;
    //   in.x.ax = 0x3000;
    //   int86(0x21, &in, &out);
    //   printf("%u\n", out.x.ax);
    // Ожидаемый фрагмент main.c:
    //   union REGS var1, var2;
    //   var1.x.ax = 12288;
    //   int86(33, &var1, &var2);
    //   printf("%u\n", var2.x.ax);
    [Fact(Skip = "NotImplemented")]
    public void Decompile_Int86Gs_DeclaresUnionRegsAndFieldAccess()
    {
        var mainSource = DecompileMainSource(ExeProvider.Get("int86.c"));

        Assert.Contains("union REGS", mainSource);
        Assert.Contains("int86(", mainSource);
        Assert.Contains("&", mainSource);
        Assert.Contains(".x.ax", mainSource);
        Assert.DoesNotContain("char var", mainSource);
        Assert.DoesNotContain("varSS", mainSource);
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

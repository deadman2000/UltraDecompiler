using TestSupport;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция примеров со структурами из заголовков QuickC (<c>dos.h</c> и т.п.).</summary>
public class DosStructDecompileTests
{
    // Исходник QuickC/PROGRAMS/dos.c:
    //   struct dosdate_t d;
    //   _dos_getdate(&d);
    //   printf("%u/%u/%u\n", d.month, d.day, d.year);
    [Fact(Skip = "NotImplemented")]
    public void Decompile_DosGs_DeclaresDosdateStructAndFieldAccess()
    {
        AssertDosdateMain(DecompileMainSource(ExeProvider.Get("dos.c")));
    }

    // Исходник QuickC/PROGRAMS/dvars.c — то же, что dos.c, но с int-локалями до и после struct.
    [Fact(Skip = "NotImplemented")]
    public void Decompile_DosWithExtraLocals_DeclaresDosdateStructAndFieldAccess()
    {
        var mainSource = DecompileMainSource(ExeProvider.Get("dvars.c"));
        AssertDosdateMain(mainSource);
        Assert.Matches(@"int var\d+;", mainSource);
        Assert.True(CountOccurrences(mainSource, "int var") >= 2, "Ожидаются дополнительные int-локали до и после struct.");
    }

    /// <summary>Тип struct и вызов API; поля в printf — после CFG/типизации, не через post-hoc printf-хак.</summary>
    private static void AssertDosdateMain(string mainSource)
    {
        Assert.Contains("struct dosdate_t", mainSource);
        Assert.Contains("_dos_getdate(&", mainSource);
        Assert.Contains("printf(", mainSource);
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
        var result = DecompileTestHelper.DecompileExe(exePath);

        Assert.True(result.Success);

        return DecompileTestHelper.ReadGeneratedFile(
            result,
            static fileName => fileName.EndsWith("main.c", StringComparison.Ordinal));
    }
}

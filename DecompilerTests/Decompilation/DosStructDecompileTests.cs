using TestSupport;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция примеров со структурами из заголовков QuickC (<c>dos.h</c> и т.п.).</summary>
public class DosStructDecompileTests
{
    // Исходник QuickC/PROGRAMS/dos.c:
    //   struct dosdate_t d;
    //   _dos_getdate(&d);
    //   printf("%u/%u/%u\n", d.month, d.day, d.year);
    // Ожидаемый фрагмент main.c:
    //   struct dosdate_t var1;
    //   _dos_getdate(&var1);
    //   printf("%u/%u/%u\n", var1.month, var1.day, var1.year);
    [Fact]
    public void Decompile_DosGs_DeclaresDosdateStructAndFieldAccess()
    {
        AssertDosdateMain(DecompileMainSource(ExeProvider.Get("dos.c")));
    }

    // Исходник QuickC/PROGRAMS/dvars.c — то же, что dos.c, но с int-локалями до и после struct.
    // Проверяем, что дополнительные int не ломают вывод типа struct и доступ к полям.
    // Ожидаемый фрагмент main.c:
    //   int var1;
    //   struct dosdate_t var2;
    //   int var3;
    //   _dos_getdate(&var2);
    //   printf("%u/%u/%u\n", var2.month, var2.day, var2.year);
    [Fact]
    public void Decompile_DosWithExtraLocals_DeclaresDosdateStructAndFieldAccess()
    {
        var mainSource = DecompileMainSource(ExeProvider.Get("dvars.c"));
        AssertDosdateMain(mainSource);
        // Две отдельные int-локали (до и после struct), не сливаются в один тип
        Assert.Matches(@"int var\d+;", mainSource);
        Assert.True(CountOccurrences(mainSource, "int var") >= 2, "Ожидаются дополнительные int-локали до и после struct.");
    }

    /// <summary>Общие проверки восстановления <c>struct dosdate_t</c> и обращений к полям.</summary>
    private static void AssertDosdateMain(string mainSource)
    {
        // Тип из dos.h, а не сырой char[] или анонимный struct
        Assert.Contains("struct dosdate_t", mainSource);
        // Вызов API DOS с указателем на локальную struct
        Assert.Contains("_dos_getdate(&", mainSource);
        // Поля восстановлены по смещениям из заголовка, а не через temp-переменные
        Assert.Contains(".month", mainSource);
        Assert.Contains(".day", mainSource);
        Assert.Contains(".year", mainSource);
        // Нет промежуточных temp-локалей и сегментных обращений к стеку
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

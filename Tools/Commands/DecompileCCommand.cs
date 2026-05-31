using McMaster.Extensions.CommandLineUtils;
using UltraDecompiler.Decompilation;

namespace Tools.Commands;

/// <summary>
/// Полная декомпиляция через <see cref="Decompiler"/>: сопоставление с .LIB,
/// сбор процедур и сохранение C-файлов.
/// </summary>
internal static class DecompileCCommand
{
    public static void Configure(CommandLineApplication root)
    {
        root.Command("decompile-c", cmd =>
        {
            cmd.Description =
                "Сопоставление с OMF .LIB, рекурсивный сбор функций и сохранение C-кода в каталог";

            var exePathArg = cmd.Argument("exePath", "Путь к .EXE или .COM").IsRequired();

            var libDirOpt = cmd.Option(
                "-l|--lib-dir <DIR>",
                "Каталог с OMF .LIB (по умолчанию — QuickC в корне репозитория)",
                CommandOptionType.SingleValue);

            var outputDirOpt = cmd.Option(
                "-o|--output-dir <DIR>",
                "Каталог для *.c (по умолчанию — каталог EXE)",
                CommandOptionType.SingleValue);

            cmd.OnExecute(() =>
            {
                var exePath = exePathArg.Value
                    ?? throw new InvalidOperationException("Не указан путь к файлу.");

                return Execute(exePath, libDirOpt.Value(), outputDirOpt.Value());
            });
        });
    }

    private static int Execute(string exePath, string? libDir, string? outputDir)
    {
        try
        {
            var libDirectory = ResolveLibraryDirectory(libDir);
            var outputDirectory = ResolveOutputDirectory(exePath, outputDir);

            var decompiler = new Decompiler();
            var result = decompiler.Decompile(exePath, libDirectory, outputDirectory);

            if (!result.Success)
            {
                Console.WriteLine("Не найдена библиотека с crt0/__astart и вызовом _main. Декомпиляция отменена.");
                return 1;
            }

            Console.WriteLine("подключаемые .LIB:");
            foreach (var libName in result.LinkedLibraryFileNames)
            {
                Console.WriteLine($"  {libName}");
            }

            Console.WriteLine();
            WriteProcedureTable(result.Procedures);

            var userCount = result.Procedures.All.Count(static p => !p.IsLibrary);
            Console.WriteLine();
            Console.WriteLine($"Пользовательских функций: {userCount}, сохранено C-файлов: {result.OutputFiles.Count}");
            Console.WriteLine();
            Console.WriteLine("Сохранённые файлы:");
            foreach (var filePath in result.OutputFiles)
            {
                Console.WriteLine($"  {filePath}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static string ResolveLibraryDirectory(string? libDir)
    {
        if (!string.IsNullOrWhiteSpace(libDir))
        {
            return Path.GetFullPath(libDir);
        }

        return Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "QuickC"));
    }

    private static string ResolveOutputDirectory(string exePath, string? outputDir)
    {
        if (!string.IsNullOrWhiteSpace(outputDir))
        {
            return Path.GetFullPath(outputDir);
        }

        return Path.GetDirectoryName(Path.GetFullPath(exePath)) ?? ".";
    }

    private static void WriteProcedureTable(ProcedureStorage procedures)
    {
        Console.WriteLine($"{"Смещение",10} {"Имя",-16} {"Тип",-12} {"Модуль",-10}");
        Console.WriteLine(new string('-', 52));

        foreach (var procedure in procedures.All.OrderBy(static p => p.Offset))
        {
            var kind = procedure.IsLibrary ? "библиотека" : "пользоват.";
            var module = procedure.LibraryMatch?.ModuleName ?? "—";
            var offsetText = $"0x{procedure.Offset:X4}";
            Console.WriteLine($"{offsetText,10} {procedure.Name,-16} {kind,-12} {module,-10}");
        }
    }
}

using McMaster.Extensions.CommandLineUtils;
using UltraDecompiler.CodeGeneration;

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

            var incDirOpt = cmd.Option(
                "-i|--inc-dir <DIR>",
                "Каталог с заголовками",
                CommandOptionType.SingleValue);

            var outputDirOpt = cmd.Option(
                "-o|--output-dir <DIR>",
                "Каталог для *.c (по умолчанию — каталог EXE)",
                CommandOptionType.SingleValue);

            cmd.OnExecute(() =>
            {
                var exePath = exePathArg.Value
                    ?? throw new InvalidOperationException("Не указан путь к файлу.");

                return Execute(exePath, libDirOpt.Value(), incDirOpt.Value(), outputDirOpt.Value());
            });
        });
    }

    private static int Execute(string exePath, string? libDir, string? incDir, string? outputDir)
    {
        try
        {
            var libDirectory = Utils.ResolveLibraryDirectory(libDir);
            var incDirectory = Utils.ResolveIncludeDirectory(incDir);
            var outputDirectory = Utils.ResolveOutputDirectory(exePath, outputDir);

            Utils.ClearDirectory(outputDirectory);

            var decompiler = new Decompiler(exePath, libDirectory, incDirectory, outputDirectory);
            var result = decompiler.Decompile(exportGraph: true);

            if (!result.Success)
            {
                Console.WriteLine("Не найдена библиотека с crt0/__astart и вызовом _main. Декомпиляция отменена.");
                return 1;
            }

            // Вывод всех возможных вариантов подключения (взаимозаменяемые crt + аддоны)
            if (result.PossibleLibraryConfigurations.Count > 1)
            {
                Console.WriteLine("Возможные варианты подключения библиотек:");
                for (var i = 0; i < result.PossibleLibraryConfigurations.Count; i++)
                {
                    var cfg = result.PossibleLibraryConfigurations[i];
                    var list = string.Join(" + ", cfg.LibraryFileNames);
                    var marker = cfg.PrimaryCrtLibrary is not null ? $" (crt0: {cfg.PrimaryCrtLibrary})" : "";
                    Console.WriteLine($"  Вариант {i + 1}: {list}{marker}");
                }

                Console.WriteLine();
                Console.WriteLine("Выбранный для декомпиляции набор (первый по приоритету):");
                foreach (var libName in result.LinkedLibraryFileNames)
                {
                    Console.WriteLine($"  {libName}");
                }
            }
            else
            {
                Console.WriteLine("подключаемые .LIB:");
                foreach (var libName in result.LinkedLibraryFileNames)
                {
                    Console.WriteLine($"  {libName}");
                }
            }

            Console.WriteLine();
            WriteProcedureTable(result.Procedures);

            Console.WriteLine(result.CompilerOptions);

            var userCount = result.Procedures.All.Count(static p => !p.IsLibrary);
            var sourceCount = result.OutputFiles.Count(static p => p.EndsWith(".c", StringComparison.OrdinalIgnoreCase));
            var headerCount = result.OutputFiles.Count(static p => p.EndsWith(".h", StringComparison.OrdinalIgnoreCase));
            var hasMakefile = result.OutputFiles.Any(static p =>
                p.EndsWith(MakefileGenerator.FileName, StringComparison.OrdinalIgnoreCase));
            Console.WriteLine();
            Console.WriteLine(
                $"Пользовательских функций: {userCount}, сохранено файлов: {result.OutputFiles.Count} ({sourceCount} .c, {headerCount} .h{(hasMakefile ? ", Makefile" : string.Empty)})");
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

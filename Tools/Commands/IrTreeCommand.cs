using System.Text;
using McMaster.Extensions.CommandLineUtils;
using UltraDecompiler.Common;
using UltraDecompiler.Ir.Rendering;

namespace Tools.Commands;

/// <summary>
/// Команда для текстового вывода IR-деревьев всех пользовательских процедур.
/// </summary>
internal static class IrTreeCommand
{
    public static void Configure(CommandLineApplication root)
    {
        root.Command("ir-tree", cmd =>
        {
            cmd.Description =
                "Построение IR через Decompiler и вывод деревьев пользовательских процедур (метки, goto)";

            var inputArg = cmd.Argument(
                "input",
                "Путь к .EXE/.COM или имя примера из QuickC/PROGRAMS (*.c)")
                .IsRequired();

            var buildOptions = ExampleBuildCliOptions.AddTo(cmd);

            var libDirOpt = cmd.Option(
                "-l|--lib-dir <DIR>",
                "Каталог с OMF .LIB (по умолчанию — QuickC в корне репозитория)",
                CommandOptionType.SingleValue);

            var incDirOpt = cmd.Option(
                "-i|--inc-dir <DIR>",
                "Каталог с заголовками (не используется при построении IR, для совместимости с decompile-c)",
                CommandOptionType.SingleValue);

            var outputOpt = cmd.Option(
                "-o|--output <PATH>",
                "Файл для вывода (по умолчанию — stdout)",
                CommandOptionType.SingleValue);

            var procOpt = cmd.Option(
                "--proc <NAME>",
                "Вывести только указанную процедуру (например main или sub_0123)",
                CommandOptionType.SingleValue);

            cmd.OnExecute(() =>
            {
                var input = inputArg.Value
                    ?? throw new InvalidOperationException("Не указан входной файл или пример.");

                var build = buildOptions.Parse();
                var exePath = ExampleInputResolver.Resolve(input, build, buildOptions.HasAnyValue);

                return Execute(exePath, libDirOpt.Value(), incDirOpt.Value(), outputOpt.Value(), procOpt.Value());
            });
        });
    }

    private static int Execute(
        string exePath,
        string? libDir,
        string? incDir,
        string? outputPath,
        string? procedureName)
    {
        try
        {
            var libDirectory = Utils.ResolveLibraryDirectory(libDir);
            var incDirectory = Utils.ResolveIncludeDirectory(incDir);

            var decompiler = new Decompiler(exePath, libDirectory, incDirectory, outputDirectory: null);
            var procedures = decompiler.BuildIR();

            if (procedures.Count == 0)
            {
                Console.Error.WriteLine("Не найдена библиотека с crt0/__astart и вызовом _main. Построение IR отменено.");
                return 1;
            }

            var selected = procedures
                .OrderBy(static p => p.Offset)
                .Where(p => string.IsNullOrWhiteSpace(procedureName)
                    || string.Equals(p.Name, procedureName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (selected.Count == 0)
            {
                Console.Error.WriteLine($"Процедура '{procedureName}' не найдена среди пользовательских функций.");
                return 1;
            }

            var text = RenderAll(selected, decompiler.OptimizationLevel, exePath);
            WriteOutput(text, outputPath);

            if (string.IsNullOrWhiteSpace(procedureName))
            {
                Console.Error.WriteLine($"Выведено процедур: {selected.Count} (оптимизация: {FormatOptLevel(decompiler.OptimizationLevel)})");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Ошибка: {ex.Message}");
            return 1;
        }
    }

    private static string RenderAll(
        IReadOnlyList<DisassembledProcedure> procedures,
        OptimizationLevel optimizationLevel,
        string exePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# IR: {Path.GetFileName(exePath)} ({FormatOptLevel(optimizationLevel)})");
        sb.AppendLine($"# Процедур: {procedures.Count}");
        sb.AppendLine();

        for (var i = 0; i < procedures.Count; i++)
        {
            var procedure = procedures[i];
            if (i > 0)
            {
                sb.AppendLine();
                sb.AppendLine(new string('-', 72));
                sb.AppendLine();
            }

            sb.AppendLine($"=== {procedure.Name} @ 0x{procedure.Offset:X4} ===");
            sb.AppendLine(IrTreeTextRenderer.RenderProcedure(procedure, optimizationLevel));
        }

        return sb.ToString();
    }

    private static void WriteOutput(string text, string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Console.Write(text);
            if (!text.EndsWith('\n'))
            {
                Console.WriteLine();
            }

            return;
        }

        var fullPath = Path.GetFullPath(outputPath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(fullPath, text);
        Console.Error.WriteLine($"IR сохранён: {fullPath}");
    }

    private static string FormatOptLevel(OptimizationLevel level) =>
        level switch
        {
            OptimizationLevel.Disabled => "/Od",
            OptimizationLevel.EnabledFull => "/Ox",
            OptimizationLevel.Enabled => "/Ot",
            OptimizationLevel.EnableLoop => "/Ol",
            _ => level.ToString(),
        };
}

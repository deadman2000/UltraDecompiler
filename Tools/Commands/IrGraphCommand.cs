using McMaster.Extensions.CommandLineUtils;
using TestSupport;
using UltraDecompiler.Compilation;

namespace Tools.Commands;

/// <summary>
/// Команда для генерации DOT-файлов с IR-деревьями (визуализация циклов и других конструкций).
/// </summary>
internal static class IrGraphCommand
{
    public static void Configure(CommandLineApplication root)
    {
        root.Command("ir-graph", cmd =>
        {
            var fileOption = cmd.Option(
                "-f|--file <PATH>",
                "Путь к EXE-файлу",
                CommandOptionType.SingleValue);

            var offsetOption = cmd.Option(
                "-o|--offset <OFFSET>",
                "Смещение начала функции в EXE (hex: 0x10)",
                CommandOptionType.SingleValue);

            var sourceOption = cmd.Option(
                "-s|--source <NAME>",
                "Имя .c файла из QuickC/PROGRAMS (EXE будет собран автоматически)",
                CommandOptionType.SingleValue);

            var outputOption = cmd.Option(
                "--out <PATH>",
                "Путь для выходного .dot файла (по умолчанию: ir_graph.dot)",
                CommandOptionType.SingleValue);

            var pngOption = cmd.Option(
                "--png",
                "Автоматически сгенерировать PNG через Graphviz (требуется dot в PATH)",
                CommandOptionType.NoValue);

            var optLevelOption = cmd.Option(
                "--opt <LEVEL>",
                "Уровень оптимизации: Od (по умолчанию) или Ox",
                CommandOptionType.SingleValue);

            cmd.OnExecute(() => Execute(
                fileOption.Value(),
                offsetOption.Value(),
                sourceOption.Value(),
                outputOption.Value(),
                pngOption.HasValue(),
                optLevelOption.Value()));
        });
    }

    private static int Execute(
        string? exePath,
        string? offsetStr,
        string? sourceName,
        string? outputPath,
        bool generatePng,
        string? optLevel)
    {
        if (string.IsNullOrEmpty(exePath) && string.IsNullOrEmpty(sourceName))
        {
            Console.Error.WriteLine("Ошибка: укажите -f|--file или -s|--source");
            return 1;
        }

        if (!string.IsNullOrEmpty(exePath) && !string.IsNullOrEmpty(sourceName))
        {
            Console.Error.WriteLine("Ошибка: укажите только один из параметров: -f|--file или -s|--source");
            return 1;
        }

        if (string.IsNullOrEmpty(offsetStr))
        {
            Console.Error.WriteLine("Ошибка: укажите -o|--offset");
            return 1;
        }

        var opt = ParseOptimizationLevel(optLevel);
        DosExeParser parser;

        if (!string.IsNullOrEmpty(sourceName))
        {
            var memoryModel = MemoryModel.Small;
            var stackCheck = false;
            var libraries = new[] { "SLIBCE.LIB" };

            try
            {
                exePath = ExeProvider.Get(
                    sourceName,
                    memoryModel,
                    stackCheck,
                    opt,
                    libraries);

                parser = new DosExeParser(exePath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Ошибка при получении EXE через ExeProvider: {ex.Message}");
                return 1;
            }
        }
        else
        {
            parser = new DosExeParser(exePath!);
        }

        int offset;
        try
        {
            offset = ParseOffset(offsetStr);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Ошибка: неверное смещение '{offsetStr}': {ex.Message}");
            return 1;
        }

        if (offset < 0 || offset >= parser.Image.Length)
        {
            Console.Error.WriteLine(
                $"Ошибка: смещение 0x{offset:X} вне образа (0..0x{parser.Image.Length - 1:X})");
            return 1;
        }

        if (!FunctionStartValidator.IsFunctionStart(parser, offset))
        {
            Console.Error.WriteLine(
                $"Ошибка: смещение 0x{offset:X} не указывает на начало функции (нет пролога push bp; mov bp, sp / enter)");
            return 1;
        }

        var disassembler = new X86Disassembler(parser.Image, parser.RelocationTable);
        var cfg = new ControlFlowGraph();
        cfg.Build(disassembler, offset, parser.IsCom ? RegisterState.InitCom : RegisterState.InitExe);

        var builder = ExpressionBuilder.Create(cfg, opt);
        builder.Build();

        string dotPath = outputPath ?? "ir_graph.dot";

        var dir = Path.GetDirectoryName(dotPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        builder.SaveDot(dotPath, false);

        Console.WriteLine($"IR-граф сохранён: {Path.GetFullPath(dotPath)}");

        if (generatePng)
        {
            GeneratePng(dotPath);
        }

        return 0;
    }

    private static int ParseOffset(string offsetStr)
    {
        var text = offsetStr.Trim();

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToInt32(text[2..], 16);
        }

        if (text.EndsWith('h') || text.EndsWith('H'))
        {
            return Convert.ToInt32(text[..^1], 16);
        }

        if (int.TryParse(text, out var decimalValue))
        {
            return decimalValue;
        }

        return Convert.ToInt32(text, 16);
    }

    private static OptimizationLevel ParseOptimizationLevel(string? optLevel)
    {
        return optLevel?.ToLowerInvariant() switch
        {
            "ox" => OptimizationLevel.EnabledFull,
            "od" => OptimizationLevel.Disabled,
            _ => OptimizationLevel.Disabled,
        };
    }

    private static void GeneratePng(string dotPath)
    {
        try
        {
            var pngPath = Path.ChangeExtension(dotPath, ".png");
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dot",
                Arguments = $"-Tpng \"{dotPath}\" -o \"{pngPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = System.Diagnostics.Process.Start(psi);
            process!.WaitForExit();

            if (process.ExitCode == 0)
            {
                Console.WriteLine($"PNG сгенерирован: {Path.GetFullPath(pngPath)}");
            }
            else
            {
                var error = process.StandardError.ReadToEnd();
                Console.Error.WriteLine($"Ошибка Graphviz: {error}");
                Console.Error.WriteLine("Убедитесь, что Graphviz установлен и dot в PATH");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Не удалось запустить Graphviz: {ex.Message}");
            Console.Error.WriteLine("Убедитесь, что Graphviz установлен (https://graphviz.org/)");
        }
    }
}

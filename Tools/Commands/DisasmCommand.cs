using McMaster.Extensions.CommandLineUtils;
using UltraDecompiler.LibMatching;

namespace Tools.Commands;

/// <summary>
/// Простое дизассемблирование DOS .EXE / .COM с возможностью указания смещения или _main.
/// </summary>
internal static class DisasmCommand
{
    public static void Configure(CommandLineApplication root)
    {
        root.Command("disasm", cmd =>
        {
            cmd.Description = "Дизассемблирование .EXE/.COM (простой вывод инструкций)";

            var exePathArg = cmd.Argument("exePath", "Путь к .EXE или .COM").IsRequired();

            var offsetOpt = cmd.Option(
                "-o|--offset <OFFSET>",
                "Стартовое смещение (hex: 0x100 / 100h или decimal). По умолчанию — точка входа",
                CommandOptionType.SingleValue);

            var mainOpt = cmd.Option(
                "--main",
                "Дизассемблировать с _main (сопоставление crt0/.LIB, как в decompile-main)",
                CommandOptionType.NoValue);

            var libDirOpt = cmd.Option(
                "-l|--lib-dir <DIR>",
                "Каталог с OMF .LIB для --main (по умолчанию — QuickC в корне репозитория)",
                CommandOptionType.SingleValue);

            var countOpt = cmd.Option(
                "-c|--count <N>",
                "Максимальное количество инструкций для вывода",
                CommandOptionType.SingleValue);

            var bytesOpt = cmd.Option(
                "-b|--bytes <N>",
                "Максимальное количество байт для дизассемблирования",
                CommandOptionType.SingleValue);

            var noColorOpt = cmd.Option(
                "--no-color",
                "Отключить ANSI-цвета в выводе",
                CommandOptionType.NoValue);

            cmd.OnExecute(() =>
            {
                var exePath = exePathArg.Value
                    ?? throw new InvalidOperationException("Не указан путь к файлу.");

                return Execute(
                    exePath,
                    offsetOpt.Value(),
                    mainOpt.HasValue(),
                    libDirOpt.Value(),
                    countOpt.Value(),
                    bytesOpt.Value(),
                    noColorOpt.HasValue());
            });
        });
    }

    private static int Execute(
        string exePath,
        string? offsetText,
        bool fromMain,
        string? libDir,
        string? countText,
        string? bytesText,
        bool noColor)
    {
        try
        {
            if (fromMain && offsetText is not null)
            {
                throw new ArgumentException("Нельзя одновременно указывать --main и --offset.");
            }

            var parser = new DosExeParser(exePath);
            parser.PrintInfo();

            var initRegisters = parser.IsCom ? RegisterState.InitCom : RegisterState.InitExe;
            var startOffset = ResolveStartOffset(parser, offsetText, fromMain, libDir, initRegisters);
            var maxInstructions = ParsePositiveInt(countText, nameof(countText));
            var maxBytes = ParsePositiveInt(bytesText, nameof(bytesText));

            if (fromMain)
            {
                Console.WriteLine($"\nДизассемблирование с _main: 0x{startOffset:X} (точка входа: 0x{parser.EntryPointOffset:X})");
            }
            else if (offsetText is not null)
            {
                Console.WriteLine($"\nДизассемблирование с 0x{startOffset:X} (точка входа: 0x{parser.EntryPointOffset:X})");
            }
            else
            {
                Console.WriteLine($"\nДизассемблирование с точки входа 0x{startOffset:X}");
            }

            Console.WriteLine();

            List<Instruction> instructions = X86Disassembler.Disassemble(
                parser.Image,
                parser.RelocationTable,
                startOffset,
                initRegisters);

            // Применяем лимиты после рекурсивного дизассемблирования (если указаны)
            if (maxBytes is int maxB)
            {
                instructions = instructions
                    .TakeWhile(i => i.Offset + i.Size <= startOffset + maxB)
                    .ToList();
            }

            if (maxInstructions is int maxI)
            {
                instructions = instructions.Take(maxI).ToList();
            }

            foreach (var instr in instructions)
            {
                var text = noColor ? instr.ToString() : instr.ToColoredString();
                Console.WriteLine(text);
            }

            if (instructions.Count == 0)
            {
                Console.WriteLine("(нет инструкций)");
            }
            else
            {
                var last = instructions[^1];
                var endAddr = last.Offset + last.Size;
                Console.WriteLine($"\nВыведено инструкций: {instructions.Count}, последний байт: 0x{endAddr - 1:X}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int ResolveStartOffset(
        DosExeParser parser,
        string? offsetText,
        bool fromMain,
        string? libDir,
        RegisterState initRegisters)
    {
        if (fromMain)
        {
            var entryPoint = (int)parser.EntryPointOffset;
            var libDirectory = Utils.ResolveLibraryDirectory(libDir);
            var provider = new LibraryProvider(libDirectory);

            if (!provider.TryResolveMain(
                    parser.Image,
                    parser.RelocationTable,
                    initRegisters,
                    entryPoint,
                    out var resolution))
            {
                throw new InvalidOperationException(
                    "Не найдена библиотека с crt0/__astart и вызовом _main.");
            }

            Console.WriteLine($"Библиотека: {resolution.PrimaryLibrary.FileName}");
            return resolution.MainOffset;
        }

        return ParseOffset(offsetText, parser);
    }

    /// <summary>Разбор смещения: десятичное, 0x… или …h. По умолчанию — EntryPointOffset.</summary>
    private static int ParseOffset(string? offsetText, DosExeParser parser)
    {
        if (string.IsNullOrWhiteSpace(offsetText))
        {
            return (int)parser.EntryPointOffset;
        }

        var text = offsetText.Trim();
        int value;

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            value = Convert.ToInt32(text[2..], 16);
        }
        else if (text.EndsWith("h", StringComparison.OrdinalIgnoreCase))
        {
            value = Convert.ToInt32(text[..^1], 16);
        }
        else if (int.TryParse(text, out value))
        {
            // десятичное
        }
        else
        {
            value = Convert.ToInt32(text, 16);
        }

        if (value < 0 || value >= parser.Image.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offsetText),
                value,
                $"Смещение должно быть в диапазоне 0..{parser.Image.Length - 1} (0x{parser.Image.Length - 1:X}).");
        }

        return value;
    }

    private static int? ParsePositiveInt(string? text, string paramName)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (!int.TryParse(text, out var value) || value <= 0)
        {
            throw new ArgumentException($"Значение {paramName} должно быть положительным целым числом.", paramName);
        }

        return value;
    }
}

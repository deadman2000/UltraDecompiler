using McMaster.Extensions.CommandLineUtils;
using UltraDecompiler.Disassembler;
using UltraDecompiler.Parser;

namespace Tools.Commands;

/// <summary>Декомпиляция DOS .EXE / .COM: дизассемблирование, CFG, символическое выполнение.</summary>
internal static class DecompileCommand
{
    public static void Configure(CommandLineApplication root)
    {
        root.Command("decompile", cmd =>
        {
            cmd.Description = "Парсинг, дизассемблирование, CFG и ExpressionBuilder";

            var exePathArg = cmd.Argument("exePath", "Путь к .EXE или .COM").IsRequired();

            var offsetOpt = cmd.Option(
                "-o|--offset <OFFSET>",
                "Стартовое смещение (hex: 0x100 или 100h; по умолчанию — точка входа)",
                CommandOptionType.SingleValue);

            cmd.OnExecute(() =>
            {
                var exePath = exePathArg.Value
                    ?? throw new InvalidOperationException("Не указан путь к файлу.");

                return Execute(exePath, offsetOpt.Value());
            });
        });
    }

    private static int Execute(string exePath, string? offsetText)
    {
        try
        {
            var parser = new DosExeParser(exePath);
            parser.PrintInfo();

            var startOffset = ParseStartOffset(offsetText, parser);

            if (offsetText is not null)
            {
                Console.WriteLine($"\nСтартовое смещение: 0x{startOffset:X} (точка входа: 0x{parser.EntryPointOffset:X})");
            }
            else
            {
                Console.WriteLine("\n=== Disassembly from entry point ===");
            }

            var outputDir = Path.GetDirectoryName(exePath) ?? ".";
            return DecompilePipeline.Run(parser, startOffset, outputDir);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>Разбор смещения: десятичное, 0x… или …h.</summary>
    private static int ParseStartOffset(string? offsetText, DosExeParser parser)
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
}

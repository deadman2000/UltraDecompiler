using System.Diagnostics;
using McMaster.Extensions.CommandLineUtils;
using UltraDecompiler.Decompilation;
using UltraDecompiler.Decompilation.Operations;
using UltraDecompiler.Disassembler;
using UltraDecompiler.Graph;
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

            var initRegisterState = parser.IsCom ? RegisterState.InitCom : RegisterState.InitExe;

            var startOffset = ParseStartOffset(offsetText, parser);

            if (offsetText is not null)
            {
                Console.WriteLine($"\nСтартовое смещение: 0x{startOffset:X} (точка входа: 0x{parser.EntryPointOffset:X})");
            }

            Console.WriteLine(
                offsetText is null
                    ? "\n=== Disassembly from entry point ==="
                    : $"\n=== Disassembly from offset 0x{startOffset:X} ===");

            var disassembler = new X86Disassembler(parser.Image, parser.RelocationTable);
            disassembler.Disassemble(startOffset, initRegisterState);

            var next = 0;
            foreach (var instr in disassembler.Instructions)
            {
                if (instr.Offset < next)
                {
                    Console.WriteLine($"Wrong instruction: {instr}");
                    return 1;
                }

                Console.WriteLine(instr.ToColoredString());
                next = instr.Offset + instr.Bytes.Length;
            }

            Console.WriteLine("\n=== Control Flow Graph ===");
            var cfg = new ControlFlowGraph();
            cfg.Build(disassembler, startOffset, initRegisterState);

            var outputDir = Path.GetDirectoryName(exePath) ?? ".";

            var cfgDotPath = Path.Combine(outputDir, "asm.dot");
            var cfgSvgPath = Path.Combine(outputDir, "asm.svg");
            cfg.SaveDot(cfgDotPath);
            ConvertDotToSvg(cfgDotPath, cfgSvgPath);
            Console.WriteLine($"CFG: {cfgDotPath}, {cfgSvgPath}");

            var expressions = new ExpressionBuilder();
            expressions.Build(cfg, parser.IsCom);

            var exprDotPath = Path.Combine(outputDir, "expr.dot");
            var exprSvgPath = Path.Combine(outputDir, "expr.svg");
            expressions.SaveDot(exprDotPath);
            ConvertDotToSvg(exprDotPath, exprSvgPath);
            Console.WriteLine($"Expressions: {exprDotPath}, {exprSvgPath}");

            var operations = expressions.GetAllOperations();
            Console.WriteLine();
            foreach (var op in operations)
            {
                Console.WriteLine(op.ToCString(asStatement: true));
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static void ConvertDotToSvg(string dotPath, string svgPath)
    {
        var proc = new Process();
        proc.StartInfo = new ProcessStartInfo("dot", $"-Tsvg \"{dotPath}\" -o \"{svgPath}\"")
        {
            UseShellExecute = false,
        };
        proc.Start();
        proc.WaitForExit();
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

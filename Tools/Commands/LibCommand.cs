using LibParser.Omf;
using McMaster.Extensions.CommandLineUtils;
using UltraDecompiler.Disassembler;

namespace Tools.Commands;

/// <summary>Разбор OMF-библиотек QuickC (.LIB).</summary>
internal static class LibCommand
{
    public static void Configure(CommandLineApplication root)
    {
        root.Command("lib", cmd =>
        {
            cmd.Description = "Парсинг библиотеки Microsoft QuickC (OMF .LIB)";

            var libPathArg = cmd.Argument("libPath", "Путь к .LIB").IsRequired();

            var listModulesOpt = cmd.Option(
                "-l|--list-modules",
                "Вывести список всех модулей",
                CommandOptionType.NoValue);

            var symbolOpt = cmd.Option(
                "-s|--symbol <NAME>",
                "Найти публичный символ и показать модуль",
                CommandOptionType.SingleValue);

            cmd.OnExecute(() =>
            {
                var libPath = libPathArg.Value
                    ?? throw new InvalidOperationException("Не указан путь к .LIB.");

                return Execute(
                    libPath,
                    listModulesOpt.HasValue(),
                    symbolOpt.Value());
            });
        });
    }

    private static int Execute(string libPath, bool listModules, string? symbolName)
    {
        try
        {
            var lib = OmfLibraryParser.ParseFile(libPath);

            Console.WriteLine($"Файл: {libPath}");
            Console.WriteLine($"Страница: {lib.Header.PageSize} байт, словарь: {lib.Header.DictionaryBlockCount} блоков");
            Console.WriteLine($"Модулей: {lib.Modules.Count}, символов: {lib.Symbols.Count}");

            if (listModules)
            {
                Console.WriteLine();
                Console.WriteLine($"{"Header",-24} {"LIBMOD",-20} {"Page",6} {"Offset",8}");
                foreach (var module in lib.Modules)
                {
                    Console.WriteLine(
                        $"{module.HeaderName,-24}{module.DisplayName,-20}{module.PageNumber,6}{module.FileOffset,8:X}");
                }
            }

            if (!string.IsNullOrEmpty(symbolName))
            {
                Console.WriteLine();
                if (!lib.Symbols.TryGetValue(symbolName, out var entry))
                {
                    Console.WriteLine($"Символ не найден: {symbolName}");
                    return 1;
                }

                var module = lib.FindModuleBySymbol(symbolName);
                if (module is null)
                {
                    Console.WriteLine(
                        $"Символ {symbolName} в словаре (страница {entry.ModulePage}), но модуль не найден.");
                    return 1;
                }

                Console.WriteLine($"Символ: {symbolName}");
                Console.WriteLine($"  Страница словаря: {entry.ModulePage}");
                Console.WriteLine($"  Модуль: {module.DisplayName} ({module.HeaderName})");
                Console.WriteLine($"  Смещение: 0x{module.FileOffset:X}");

                foreach (var seg in module.Segments)
                {
                    Console.WriteLine(
                        $"  Сегмент [{seg.SegmentIndex}] {seg.SegmentName} ({seg.ClassName}): {seg.Data.Length} байт");
                }

                var code = module.CodeSegments.FirstOrDefault();
                if (code is not null)
                {
                    Console.WriteLine($"  Код (CODE): {code.Data.Length} байт");

                    var disassembler = new X86Disassembler(code.Data);
                    disassembler.Disassemble(0);

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
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}

using LibParser.Models;
using LibParser.Omf;
using McMaster.Extensions.CommandLineUtils;

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
                "Вывести список всех модулей и их публичных символов из словаря",
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
                var symbolsByPage = BuildSymbolsByPage(lib.Symbols);
                Console.WriteLine();
                Console.WriteLine($"{"Header",-24} {"LIBMOD",-20} {"Page",6} {"Offset",8} {"Symbols",7}");
                foreach (var module in lib.Modules)
                {
                    symbolsByPage.TryGetValue(module.PageNumber, out var moduleSymbols);
                    var symbolCount = moduleSymbols?.Count ?? 0;
                    Console.WriteLine(
                        $"{module.HeaderName,-24}{module.DisplayName,-20}{module.PageNumber,6}{module.FileOffset,8:X}{symbolCount,7}");

                    if (moduleSymbols is { Count: > 0 })
                    {
                        foreach (var name in moduleSymbols)
                        {
                            Console.WriteLine($"    {name}");
                        }
                    }
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

                WriteFixupsTable(module.Fixups);

                var code = module.CodeSegments.FirstOrDefault();
                if (code is not null)
                {
                    Console.WriteLine($"  Код (CODE): {code.Data.Length} байт");

                    var codeOffset = module.TryGetCodeOffset(symbolName) ?? 0;
                    if (codeOffset != 0)
                    {
                        Console.WriteLine($"  Точка входа: 0x{codeOffset:X}");
                    }
                    else if (module.PublicSymbols.Count > 0)
                    {
                        Console.WriteLine(
                            $"  Предупреждение: PUBDEF для {symbolName} не найден, дизассемблирование с 0");
                    }

                    var relocationTable = OmfRelocationTableBuilder.Build(code, module.Fixups);
                    var disassembler = new X86Disassembler(code.Data, relocationTable);
                    disassembler.Disassemble(codeOffset);

                    foreach (var instr in disassembler.Instructions)
                    {
                        Console.WriteLine(instr.ToColoredString());
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

    /// <summary>Группирует публичные символы словаря по номеру страницы модуля.</summary>
    private static Dictionary<ushort, List<string>> BuildSymbolsByPage(
        IReadOnlyDictionary<string, OmfPublicSymbol> symbols)
    {
        var byPage = new Dictionary<ushort, List<string>>();
        foreach (var entry in symbols.Values)
        {
            if (!byPage.TryGetValue(entry.ModulePage, out var names))
            {
                names = [];
                byPage[entry.ModulePage] = names;
            }

            names.Add(entry.Name);
        }

        foreach (var names in byPage.Values)
        {
            names.Sort(StringComparer.Ordinal);
        }

        return byPage;
    }

    private static void WriteFixupsTable(IReadOnlyList<OmfFixup> fixups)
    {
        Console.WriteLine();
        Console.WriteLine($"  FIXUPP: {fixups.Count}");

        if (fixups.Count == 0)
        {
            return;
        }

        Console.WriteLine(
            $"  {"Offset",6} {"Seg",3} {"Type",12} {"Rel",3} {"Frame",-22} Target");

        foreach (var fixup in fixups.OrderBy(f => f.SegmentOffset))
        {
            var rel = fixup.IsSegmentRelative ? "seg" : "pc";
            Console.WriteLine(
                $"  {fixup.SegmentOffset:X4} {fixup.SegmentIndex,3} " +
                $"{fixup.LocationType,-12}{rel,3} " +
                $"{FormatFixupReference(fixup.Frame),-22}{FormatFixupReference(fixup.Target)}");
        }
    }

    private static string FormatFixupReference(OmfFixupReference reference)
    {
        var text = reference.Kind switch
        {
            OmfFixupDatumKind.Segdef => FormatIndexedReference("SEG", reference),
            OmfFixupDatumKind.Grpdef => FormatIndexedReference("GRP", reference),
            OmfFixupDatumKind.Extdef => FormatIndexedReference("EXT", reference),
            OmfFixupDatumKind.LedataSegment => "LEDATA",
            OmfFixupDatumKind.TargetFrame => "TFRAME",
            _ => "?",
        };

        if (reference.FromThread)
        {
            return $"T{reference.ThreadNumber}:{text}";
        }

        return text;
    }

    private static string FormatIndexedReference(string prefix, OmfFixupReference reference)
    {
        var name = reference.Name ?? $"#{reference.Index}";
        if (reference.Displacement == 0)
        {
            return $"{prefix} {name}";
        }

        return $"{prefix} {name}+0x{reference.Displacement:X}";
    }
}

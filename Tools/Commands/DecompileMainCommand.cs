using McMaster.Extensions.CommandLineUtils;
using UltraDecompiler.Disassembler;
using UltraDecompiler.LibMatching;
using UltraDecompiler.Parser;

namespace Tools.Commands;

/// <summary>
/// Декомпиляция с сопоставлением точки входа crt0 по OMF-библиотекам и поиском <c>_main</c>.
/// </summary>
internal static class DecompileMainCommand
{
    public static void Configure(CommandLineApplication root)
    {
        root.Command("decompile-main", cmd =>
        {
            cmd.Description =
                "Сопоставление точки входа с crt0 (.LIB), поиск _main и декомпиляция через ExpressionBuilder";

            var exePathArg = cmd.Argument("exePath", "Путь к .EXE или .COM").IsRequired();

            var libDirOpt = cmd.Option(
                "-l|--lib-dir <DIR>",
                "Каталог с OMF .LIB (по умолчанию — QuickC в корне репозитория)",
                CommandOptionType.SingleValue);

            cmd.OnExecute(() =>
            {
                var exePath = exePathArg.Value
                    ?? throw new InvalidOperationException("Не указан путь к файлу.");

                return Execute(exePath, libDirOpt.Value());
            });
        });
    }

    private static int Execute(string exePath, string? libDir)
    {
        try
        {
            var parser = new DosExeParser(exePath);
            parser.PrintInfo();

            var initRegisterState = parser.IsCom ? RegisterState.InitCom : RegisterState.InitExe;
            var entryPoint = (int)parser.EntryPointOffset;
            var libDirectory = ResolveLibraryDirectory(libDir);

            var libraries = LibMatcher.LoadLibraries(libDirectory);
            var matcher = new LibMatcher();
            var entryMatches = matcher.MatchEntryPoint(
                parser.Image,
                parser.RelocationTable,
                entryPoint,
                libraries,
                initRegisterState,
                symbolName: null,
                moduleName: LibMatcher.Crt0ModuleName);

            WriteEntryPointMatchTable(entryPoint, entryMatches);

            // Разрешаем viable + сразу выбираем предпочтительный (поиск astartOffset полностью внутри LibMatcher)
            var chosen = matcher.ResolvePreferredMain(
                parser.Image,
                parser.RelocationTable,
                entryMatches,
                initRegisterState,
                entryPoint);
            if (chosen is null)
            {
                Console.WriteLine("Не найдена библиотека с crt0/__astart и вызовом _main. Декомпиляция отменена.");
                return 1;
            }

            var (selected, astartOffset, mainOffset) = chosen.Value;

            Console.WriteLine($"Выбрана для анализа: {selected.Library.FileName}");
            Console.WriteLine($"Адрес {LibMatcher.AstartSymbol}: 0x{astartOffset:X} (линейно в образе)");
            Console.WriteLine($"Адрес main: 0x{mainOffset:X} (линейно в образе)");

            return DecompilePipeline.Run(parser, mainOffset);
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

    private static void WriteEntryPointMatchTable(
        int entryPoint,
        IReadOnlyList<EntryPointLibraryMatchInfo> entryMatches)
    {
        Console.WriteLine();
        Console.WriteLine($"{"Библиотека",-16} {"Match",7} {"__astart",9} {"Модуль",-10} CRT0-символы");
        Console.WriteLine(new string('-', 72));

        if (entryMatches.Count == 0)
        {
            Console.WriteLine("  (нет совпадений)");
            return;
        }

        foreach (var match in entryMatches)
        {
            var crt0Symbols = match.Matches
                .Where(static m => m.ModuleName.Equals("crt0", StringComparison.OrdinalIgnoreCase))
                .Select(static m => m.SymbolName)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static n => n, StringComparer.Ordinal)
                .ToList();

            var astart = match.AstartMatch is not null ? "+" : "-";
            var moduleName = match.AstartMatch?.ModuleName
                ?? (crt0Symbols.Count > 0 ? "crt0" : "—");

            Console.WriteLine(
                $"{match.Library.FileName,-16} {match.Matches.Count,5} {astart,3} {moduleName,-8} {FormatSymbolList(crt0Symbols)}");
        }

        Console.WriteLine();
        Console.WriteLine($"Точка входа EXE: 0x{entryPoint:X}");
    }

    private static string FormatSymbolList(IReadOnlyList<string> symbols)
    {
        if (symbols.Count == 0)
        {
            return "—";
        }

        const int maxShown = 6;
        if (symbols.Count <= maxShown)
        {
            return string.Join(", ", symbols);
        }

        return string.Join(", ", symbols.Take(maxShown)) + $", … (+{symbols.Count - maxShown})";
    }

}

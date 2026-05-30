using LibMatching;
using LibParser.Models;
using McMaster.Extensions.CommandLineUtils;
using UltraDecompiler.Disassembler;
using UltraDecompiler.Parser;

namespace Tools.Commands;

/// <summary>
/// Декомпиляция с сопоставлением точки входа crt0 по OMF-библиотекам и поиском <c>_main</c>.
/// </summary>
internal static class DecompileMatchCommand
{
    private const string AstartSymbol = "__astart";
    private const string MainSymbol = "_main";

    public static void Configure(CommandLineApplication root)
    {
        root.Command("decompile-match", cmd =>
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

    private static int Execute(string exePath, string? libDirText)
    {
        try
        {
            var parser = new DosExeParser(exePath);
            parser.PrintInfo();

            var initRegisterState = parser.IsCom ? RegisterState.InitCom : RegisterState.InitExe;
            var entryPoint = (int)parser.EntryPointOffset;
            var libDirectory = ResolveLibraryDirectory(libDirText);

            var entryMatches = Crt0EntryPointMatcher.MatchDirectory(
                parser.Image,
                parser.RelocationTable,
                entryPoint,
                libDirectory,
                initRegisterState);

            WriteEntryPointMatchTable(entryPoint, entryMatches);

            var resolved = ResolveLibraryAndMain(parser, entryMatches, initRegisterState, entryPoint);
            if (resolved is null)
            {
                Console.WriteLine("Не найдена библиотека с crt0/__astart и вызовом _main. Декомпиляция отменена.");
                return 1;
            }

            var (selected, astartOffset, mainOffset) = resolved.Value;

            Console.WriteLine($"библиотека: {selected.LibraryFileName}");
            Console.WriteLine($"Адрес {AstartSymbol}: 0x{astartOffset:X} (линейно в образе)");
            Console.WriteLine($"Адрес {MainSymbol}: 0x{mainOffset:X} (линейно в образе)");

            return DecompilePipeline.Run(parser, mainOffset);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static string ResolveLibraryDirectory(string? libDirText)
    {
        if (!string.IsNullOrWhiteSpace(libDirText))
        {
            return Path.GetFullPath(libDirText);
        }

        return Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "QuickC"));
    }

    private static void WriteEntryPointMatchTable(
        int entryPoint,
        IReadOnlyList<EntryPointLibraryMatch> entryMatches)
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
                $"{match.LibraryFileName,-16} {match.Matches.Count,5} {astart,3} {moduleName,-8} {FormatSymbolList(crt0Symbols)}");
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

    private static (EntryPointLibraryMatch Match, int AstartOffset, int MainOffset)? ResolveLibraryAndMain(
        DosExeParser parser,
        IReadOnlyList<EntryPointLibraryMatch> entryMatches,
        RegisterState initRegisters,
        int entryPoint)
    {
        foreach (var match in OrderLibraryCandidates(entryMatches))
        {
            try
            {
                var astartOffset = ResolveAstartOffset(parser, match.Library, initRegisters, entryPoint);
                var mainOffset = MainOffsetFinder.FindFromAstart(
                    parser.Image,
                    parser.RelocationTable,
                    match.Library,
                    astartOffset,
                    initRegisters,
                    match.AstartMatch!.ModuleCodeOffset);

                return (match, astartOffset, mainOffset);
            }
            catch (InvalidOperationException)
            {
            }
        }

        return null;
    }

    private static IEnumerable<EntryPointLibraryMatch> OrderLibraryCandidates(
        IReadOnlyList<EntryPointLibraryMatch> entryMatches) =>
        entryMatches
            .Where(static m => m.AstartMatch is not null)
            .OrderBy(static m => LibraryPriority(m.LibraryFileName))
            .ThenBy(static m => PreferEmulatorLibrary(m.LibraryFileName))
            .ThenBy(static m => MemoryModelLibraryPriority(m.LibraryFileName))
            .ThenBy(static m => m.LibraryFileName, StringComparer.OrdinalIgnoreCase);

    /// <summary>S → C → M → L: при нескольких совпадениях предпочитаем small-модель.</summary>
    private static int MemoryModelLibraryPriority(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();
        if (name.StartsWith("SLIB", StringComparison.Ordinal))
        {
            return 0;
        }

        if (name.StartsWith("CLIB", StringComparison.Ordinal))
        {
            return 1;
        }

        if (name.StartsWith("MLIB", StringComparison.Ordinal))
        {
            return 2;
        }

        if (name.StartsWith("LLIB", StringComparison.Ordinal))
        {
            return 3;
        }

        return 4;
    }

    /// <summary>QuickC по умолчанию линкует *LIBCE.LIB / *LIBC.LIB с эмулятором (суффикс E).</summary>
    private static int PreferEmulatorLibrary(string fileName) =>
        Path.GetFileNameWithoutExtension(fileName).Contains('E', StringComparison.OrdinalIgnoreCase) ? 0 : 1;

    private static int LibraryPriority(string fileName)
    {
        if (fileName.Contains("LIBC", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (fileName.Contains("LIBFP", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static int ResolveAstartOffset(
        DosExeParser parser,
        OmfLibrary library,
        RegisterState initRegisters,
        int entryPoint)
    {
        // Точка входа уже совпала с __astart/crt0 — не сканируем весь образ без необходимости.
        var epMatches = LibraryFunctionMatcher.Match(
            parser.Image,
            parser.RelocationTable,
            entryPoint,
            library,
            initRegisters);

        if (epMatches.Any(static m => m.SymbolName == AstartSymbol))
        {
            return entryPoint;
        }

        return LibrarySymbolFinder.Find(
            parser.Image,
            parser.RelocationTable,
            library,
            AstartSymbol,
            initRegisters);
    }
}

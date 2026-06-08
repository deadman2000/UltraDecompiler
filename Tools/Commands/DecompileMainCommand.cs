using Common;
using LibParser.Models;
using LibParser.Omf;
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
    private const string AstartSymbol = "__astart";
    private const string Crt0ModuleName = "crt0";

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

            var entryMatches = MatchEntryPointDirectory(
                parser.Image,
                parser.RelocationTable,
                entryPoint,
                libDirectory,
                initRegisterState);

            WriteEntryPointMatchTable(entryPoint, entryMatches);

            var viable = ResolveAllViableLibrariesAndMains(parser, entryMatches, initRegisterState, entryPoint);
            if (viable.Count == 0)
            {
                Console.WriteLine("Не найдена библиотека с crt0/__astart и вызовом _main. Декомпиляция отменена.");
                return 1;
            }

            WriteLibraryConfigurationVariants(viable, entryPoint);

            // Для последующей декомпиляции/анализа выбираем предпочтительный вариант
            var (selected, astartOffset, mainOffset) = PickPreferredViable(viable);

            Console.WriteLine($"Выбрана для анализа: {selected.Library.FileName}");
            Console.WriteLine($"Адрес {AstartSymbol}: 0x{astartOffset:X} (линейно в образе)");
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

    private static IReadOnlyList<EntryPointLibraryMatchInfo> MatchEntryPointDirectory(
        byte[] image,
        RelocationTable imageRelocations,
        int entryPointOffset,
        string libraryDirectory,
        RegisterState initRegisters)
    {
        if (!Directory.Exists(libraryDirectory))
        {
            throw new DirectoryNotFoundException($"Каталог библиотек не найден: {libraryDirectory}");
        }

        var results = new List<EntryPointLibraryMatchInfo>();

        foreach (var libraryPath in Directory.EnumerateFiles(libraryDirectory, "*.LIB").OrderBy(static p => p))
        {
            var library = OmfLibraryParser.ParseFile(libraryPath);
            var matches = LibraryFunctionMatcher.Match(
                image,
                imageRelocations,
                entryPointOffset,
                library,
                initRegisters,
                symbolName: null,
                moduleName: Crt0ModuleName);

            if (matches.Count == 0)
            {
                continue;
            }

            results.Add(new EntryPointLibraryMatchInfo
            {
                Library = library,
                Matches = matches.Select(m => new LibraryMatchInfo
                {
                    SymbolName = m.SymbolName,
                    ModulePage = m.ModulePage,
                    ModuleName = m.ModuleName,
                    ModuleCodeOffset = m.ModuleCodeOffset,
                    LibraryFileName = library.FileName,
                }).ToList(),
            });
        }

        return results;
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

    /// <summary>
    /// Возвращает ВСЕ viable библиотеки, для которых:
    /// - есть совпадение __astart/crt0 на входе
    /// - успешно удалось разрешить адрес _main по FIXUPP из метаданных этой библиотеки.
    /// Каждый такой — самостоятельный кандидат на "главную" библиотеку.
    /// </summary>
    private static List<(EntryPointLibraryMatchInfo Match, int AstartOffset, int MainOffset)> ResolveAllViableLibrariesAndMains(
        DosExeParser parser,
        IReadOnlyList<EntryPointLibraryMatchInfo> entryMatches,
        RegisterState initRegisters,
        int entryPoint)
    {
        var viable = new List<(EntryPointLibraryMatchInfo Match, int AstartOffset, int MainOffset)>();

        foreach (var match in OrderLibraryCandidates(entryMatches))
        {
            try
            {
                var astartOffset = ResolveAstartOffset(parser, match.Library, initRegisters, entryPoint);
                var mainOffset = LibraryCallResolver.FindMainFromAstart(
                    parser.Image,
                    parser.RelocationTable,
                    match.Library,
                    astartOffset,
                    initRegisters,
                    match.AstartMatch!.ModuleCodeOffset);

                viable.Add((match, astartOffset, mainOffset));
            }
            catch (InvalidOperationException)
            {
                // crt0 этой библиотеки не соответствует вызову _main в образе — не кандидат
            }
        }

        return viable;
    }

    private static void WriteLibraryConfigurationVariants(
        IReadOnlyList<(EntryPointLibraryMatchInfo Match, int AstartOffset, int MainOffset)> viable,
        int entryPoint)
    {
        Console.WriteLine();
        if (viable.Count == 0)
        {
            Console.WriteLine("(нет viable библиотек с __astart + _main)");
            return;
        }

        if (viable.Count == 1)
        {
            var v = viable[0];
            Console.WriteLine($"Библиотека (crt0): {v.Match.Library.FileName}");
            return;
        }

        Console.WriteLine("Возможные варианты подключения библиотек (crt0/__astart):");
        for (var i = 0; i < viable.Count; i++)
        {
            var v = viable[i];
            var astart = v.AstartOffset == entryPoint ? "(точка входа)" : $"0x{v.AstartOffset:X}";
            Console.WriteLine($"  Вариант {i + 1}: {v.Match.Library.FileName}   __astart@{astart}   main@0x{v.MainOffset:X}");
        }

        Console.WriteLine();
        Console.WriteLine("Примечание: эти библиотеки взаимозаменяемы по crt0 (разные модели памяти / эмуляторы).");
        Console.WriteLine("           Если в EXE используются символы из дополнительных .LIB — они будут общими для вариантов.");
    }

    private static (EntryPointLibraryMatchInfo Match, int AstartOffset, int MainOffset) PickPreferredViable(
        IReadOnlyList<(EntryPointLibraryMatchInfo Match, int AstartOffset, int MainOffset)> viable)
    {
        return viable
            .OrderBy(static v => LibraryPriority(v.Match.Library.FileName))
            .ThenBy(static v => PreferEmulatorLibrary(v.Match.Library.FileName))
            .ThenBy(static v => MemoryModelLibraryPriority(v.Match.Library.FileName))
            .ThenBy(static v => v.Match.Library.FileName, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static IEnumerable<EntryPointLibraryMatchInfo> OrderLibraryCandidates(
        IReadOnlyList<EntryPointLibraryMatchInfo> entryMatches) =>
        entryMatches
            .Where(static m => m.AstartMatch is not null)
            .OrderBy(static m => LibraryPriority(m.Library.FileName))
            .ThenBy(static m => PreferEmulatorLibrary(m.Library.FileName))
            .ThenBy(static m => MemoryModelLibraryPriority(m.Library.FileName))
            .ThenBy(static m => m.Library.FileName, StringComparer.OrdinalIgnoreCase);

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
            initRegisters,
            AstartSymbol,
            Crt0ModuleName);

        if (epMatches.Count > 0)
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

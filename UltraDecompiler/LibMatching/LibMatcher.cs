using Common;
using LibParser.Models;
using LibParser.Omf;

namespace UltraDecompiler.LibMatching;

public static class LibMatcher
{
    /// <summary>Имя символа crt0-точки входа в библиотеках QuickC.</summary>
    public const string AstartSymbol = "__astart";

    /// <summary>Имя модуля crt0 в OMF-библиотеках.</summary>
    public const string Crt0ModuleName = "crt0";

    /// <summary>
    /// Сопоставляет точку входа с символами OMF-библиотек.
    /// </summary>
    /// <param name="symbolName">Если задано — проверяется только этот символ.</param>
    /// <param name="moduleName">Если задано — проверяются только символы указанного модуля.</param>
    public static IReadOnlyList<EntryPointLibraryMatchInfo> MatchEntryPoint(
        byte[] image,
        RelocationTable imageRelocations,
        int entryPointOffset,
        IReadOnlyList<OmfLibrary> libraries,
        RegisterState initRegisters,
        string? symbolName = null,
        string? moduleName = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(imageRelocations);
        ArgumentNullException.ThrowIfNull(libraries);

        var results = new List<EntryPointLibraryMatchInfo>();

        foreach (var library in libraries)
        {
            var matches = LibraryFunctionMatcher.Match(
                image,
                imageRelocations,
                entryPointOffset,
                library,
                initRegisters,
                symbolName,
                moduleName);

            if (matches.Count == 0)
            {
                continue;
            }

            results.Add(new EntryPointLibraryMatchInfo
            {
                Library = library,
                Matches = matches.Select(m => ToMatchInfo(m, library)).ToList(),
            });
        }

        return results;
    }

    public static IReadOnlyList<LibraryMatchInfo> MatchFunction(
        byte[] image,
        RelocationTable imageRelocations,
        int imageOffset,
        OmfLibrary library,
        RegisterState initRegisters) =>
        MatchFunction(image, imageRelocations, imageOffset, library, initRegisters, symbolName: null, moduleName: null);

    /// <summary>
    /// Сопоставляет участок образа с символами одной OMF-библиотеки.
    /// </summary>
    public static IReadOnlyList<LibraryMatchInfo> MatchFunction(
        byte[] image,
        RelocationTable imageRelocations,
        int imageOffset,
        OmfLibrary library,
        RegisterState initRegisters,
        string? symbolName,
        string? moduleName) =>
        LibraryFunctionMatcher.Match(
                image,
                imageRelocations,
                imageOffset,
                library,
                initRegisters,
                symbolName,
                moduleName)
            .Select(m => ToMatchInfo(m, library))
            .ToList();

    public static int FindMainOffset(
        byte[] image,
        RelocationTable imageRelocations,
        OmfLibrary library,
        int astartOffset,
        RegisterState initRegisters,
        int astartModuleCodeOffset) =>
        LibraryCallResolver.FindMainFromAstart(
            image,
            imageRelocations,
            library,
            astartOffset,
            initRegisters,
            astartModuleCodeOffset);

    /// <summary>
    /// Универсальный поиск адреса символа по вызову из другого символа (через FIXUPP модуля библиотеки).
    /// </summary>
    public static int FindCalledSymbolOffset(
        byte[] image,
        RelocationTable imageRelocations,
        OmfLibrary library,
        string callerSymbolName,
        string targetSymbolName,
        int callerOffset,
        RegisterState initRegisters,
        int callerModuleCodeOffset = 0) =>
        LibraryCallResolver.FindCalledSymbol(
            image,
            imageRelocations,
            library,
            callerSymbolName,
            targetSymbolName,
            callerOffset,
            initRegisters,
            callerModuleCodeOffset);

    private static LibraryMatchInfo ToMatchInfo(LibraryMatchResult match, OmfLibrary library) =>
        new()
        {
            SymbolName = match.SymbolName,
            ModulePage = match.ModulePage,
            ModuleName = match.ModuleName,
            ModuleCodeOffset = match.ModuleCodeOffset,
            LibraryFileName = library.FileName,
        };

    /// <summary>
    /// Загружает все .LIB из указанного каталога (в алфавитном порядке имён файлов).
    /// </summary>
    public static List<OmfLibrary> LoadLibraries(string libraryDirectory)
    {
        if (!Directory.Exists(libraryDirectory))
        {
            throw new DirectoryNotFoundException($"Каталог библиотек не найден: {libraryDirectory}");
        }

        var libraries = new List<OmfLibrary>();
        foreach (var libraryPath in Directory.EnumerateFiles(libraryDirectory, "*.LIB").OrderBy(static p => p))
        {
            libraries.Add(OmfLibraryParser.ParseFile(libraryPath));
        }

        return libraries;
    }

    /// <summary>
    /// Разрешает линейное смещение тела __astart в образе EXE/COM по результату сопоставления точки входа.
    /// Если в <paramref name="match"/> уже есть <see cref="EntryPointLibraryMatchInfo.AstartMatch"/>,
    /// возвращает <paramref name="entryPoint"/> (оптимизация — не перепроверяем).
    /// Иначе выполняет поиск: сначала проверка на entryPoint, затем fallback через <see cref="LibrarySymbolFinder"/>.
    /// </summary>
    public static int ResolveAstartOffset(
        byte[] image,
        RelocationTable imageRelocations,
        EntryPointLibraryMatchInfo match,
        int entryPoint,
        RegisterState initRegisters)
    {
        ArgumentNullException.ThrowIfNull(match);

        if (match.AstartMatch is not null)
        {
            // Первичное сопоставление уже подтвердило __astart на этой позиции — доверяем.
            return entryPoint;
        }

        // Нет готового AstartMatch — используем общий поиск по библиотеке (ep + fallback scan)
        return ResolveAstartOffset(image, imageRelocations, match.Library, entryPoint, initRegisters);
    }

    /// <summary>
    /// Разрешает смещение __astart в образе для данной библиотеки (без предварительного EntryPointMatch).
    /// Сначала проверяет совпадение на точке входа, при необходимости делает полный перебор (fallback).
    /// </summary>
    public static int ResolveAstartOffset(
        byte[] image,
        RelocationTable imageRelocations,
        OmfLibrary library,
        int entryPoint,
        RegisterState initRegisters)
    {
        // Пытаемся на точке входа (быстрый путь, когда crt0/__astart совпал на ep)
        var epMatches = MatchFunction(
            image,
            imageRelocations,
            entryPoint,
            library,
            initRegisters,
            symbolName: AstartSymbol,
            moduleName: Crt0ModuleName);

        if (epMatches.Count > 0)
        {
            return entryPoint;
        }

        // Fallback: поиск по всему образу (для случаев, когда __astart не на точке входа, но crt0 частично совпал)
        return LibrarySymbolFinder.Find(image, imageRelocations, library, AstartSymbol, initRegisters);
    }

    /// <summary>
    /// Упорядочивает кандидатов с совпадением crt0/__astart по приоритетам библиотек
    /// (LIBC, эмулятор E, small-модель и т.д.). Используется при сборе viable для decompile-main.
    /// </summary>
    public static IEnumerable<EntryPointLibraryMatchInfo> OrderLibraryCandidates(
        IReadOnlyList<EntryPointLibraryMatchInfo> entryMatches) =>
        entryMatches
            .Where(static m => m.AstartMatch is not null)
            .OrderBy(static m => LibraryPriority(m.Library.FileName))
            .ThenBy(static m => PreferEmulatorLibrary(m.Library.FileName))
            .ThenBy(static m => MemoryModelLibraryPriority(m.Library.FileName))
            .ThenBy(static m => m.Library.FileName, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Возвращает ВСЕ viable библиотеки, для которых:
    /// - есть совпадение __astart/crt0 в entryMatches
    /// - успешно удалось разрешить адрес _main по FIXUPP из метаданных этой библиотеки
    ///   (с использованием ResolveAstartOffset + FindMainOffset).
    /// Кандидаты предварительно сортируются по приоритетам (как в decompile-main).
    /// Каждый такой — самостоятельный кандидат на "главную" библиотеку.
    /// Этот метод инкапсулирует поиск astartOffset (и main) и используется diagnostic CLI decompile-main.
    /// </summary>
    public static List<(EntryPointLibraryMatchInfo Match, int AstartOffset, int MainOffset)> ResolveAllViableLibrariesAndMains(
        byte[] image,
        RelocationTable imageRelocations,
        IReadOnlyList<EntryPointLibraryMatchInfo> entryMatches,
        RegisterState initRegisters,
        int entryPoint)
    {
        var viable = new List<(EntryPointLibraryMatchInfo Match, int AstartOffset, int MainOffset)>();

        foreach (var match in OrderLibraryCandidates(entryMatches))
        {
            try
            {
                var astartOffset = ResolveAstartOffset(
                    image,
                    imageRelocations,
                    match,
                    entryPoint,
                    initRegisters);
                var mainOffset = FindMainOffset(
                    image,
                    imageRelocations,
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

    /// <summary>
    /// Выбирает предпочтительный viable из списка (по приоритетам LIBC / эмулятор / модель памяти / имя файла).
    /// Предполагается, что список не пуст.
    /// </summary>
    public static (EntryPointLibraryMatchInfo Match, int AstartOffset, int MainOffset) PickPreferredViable(
        IReadOnlyList<(EntryPointLibraryMatchInfo Match, int AstartOffset, int MainOffset)> viable)
    {
        return viable
            .OrderBy(static v => LibraryPriority(v.Match.Library.FileName))
            .ThenBy(static v => PreferEmulatorLibrary(v.Match.Library.FileName))
            .ThenBy(static v => MemoryModelLibraryPriority(v.Match.Library.FileName))
            .ThenBy(static v => v.Match.Library.FileName, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    /// <summary>
    /// Разрешает viable библиотеки (с поиском astartOffset) и сразу выбирает предпочтительный по приоритетам.
    /// Возвращает null, если не найдено ни одного viable (нет crt0/__astart + _main).
    /// Это объединение ResolveAllViableLibrariesAndMains + PickPreferredViable для упрощения DecompileMainCommand.
    /// </summary>
    public static (EntryPointLibraryMatchInfo Match, int AstartOffset, int MainOffset)? ResolvePreferredMain(
        byte[] image,
        RelocationTable imageRelocations,
        IReadOnlyList<EntryPointLibraryMatchInfo> entryMatches,
        RegisterState initRegisters,
        int entryPoint)
    {
        var viable = ResolveAllViableLibrariesAndMains(
            image, imageRelocations, entryMatches, initRegisters, entryPoint);

        if (viable.Count == 0)
            return null;

        return PickPreferredViable(viable);
    }

    /// <summary>S → C → M → L: при нескольких совпадениях предпочитаем small-модель.</summary>
    public static int MemoryModelLibraryPriority(string fileName)
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
    public static int PreferEmulatorLibrary(string fileName) =>
        Path.GetFileNameWithoutExtension(fileName).Contains('E', StringComparison.OrdinalIgnoreCase) ? 0 : 1;

    public static int LibraryPriority(string fileName)
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
}

using Common;
using LibParser.Models;
using LibParser.Omf;
using UltraDecompiler.Compilation;

namespace UltraDecompiler.LibMatching;

/// <summary>
/// Единая точка работы с OMF-библиотеками QuickC для декомпилятора.
/// Инкапсулирует загрузку .LIB из каталога, сопоставление crt0/__astart на точке входа,
/// разрешение адреса _main, сужение кандидатов по мере сопоставления символов и
/// матчинг runtime-функций во время сбора процедур.
/// </summary>
/// <remarks>
/// Decompiler использует только высокоуровневый API:
/// - конструктор с путём к папке библиотек,
/// - TryResolveMain для получения адреса main и информации о вариантах библиотек,
/// - TryMatchProcedure для сопоставления других процедур (с автоматическим narrowing кандидатов).
/// Вся сложная логика MatchEntryPoint / NarrowBy* / ResolveAll* / PickPreferred / ConfirmAstart спрятана внутри.
/// </remarks>
public sealed class LibraryProvider
{
    private readonly List<OmfLibrary> _allLibraries;
    private readonly List<OmfLibrary> _candidates;
    private readonly List<OmfLibrary> _linked = [];

    /// <summary>Имя символа crt0-точки входа.</summary>
    public const string AstartSymbol = "__astart";

    /// <summary>Имя модуля crt0.</summary>
    public const string Crt0ModuleName = "crt0";

    /// <summary>
    /// Загружает все .LIB из указанного каталога (в алфавитном порядке имён файлов).
    /// Эквивалент прежнего LibMatcher.LoadLibraries.
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
    /// Создаёт провайдер и загружает все *.LIB из указанного каталога (в алфавитном порядке).
    /// </summary>
    public LibraryProvider(string libraryDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryDirectory);

        if (!Directory.Exists(libraryDirectory))
        {
            throw new DirectoryNotFoundException($"Каталог библиотек не найден: {libraryDirectory}");
        }

        _allLibraries = new List<OmfLibrary>();
        foreach (var libraryPath in Directory.EnumerateFiles(libraryDirectory, "*.LIB").OrderBy(static p => p))
        {
            _allLibraries.Add(OmfLibraryParser.ParseFile(libraryPath));
        }

        if (_allLibraries.Count == 0)
        {
            throw new DirectoryNotFoundException($"В каталоге {libraryDirectory} не найдено файлов *.LIB.");
        }

        _candidates = new List<OmfLibrary>(_allLibraries);
    }

    /// <summary>Все загруженные библиотеки (не сужаются).</summary>
    public IReadOnlyList<OmfLibrary> AllLibraries => _allLibraries;

    /// <summary>Текущие кандидаты (ещё не исключённые).</summary>
    public IReadOnlyList<OmfLibrary> Candidates => _candidates;

    /// <summary>Библиотеки, из которых уже был подтверждён хотя бы один символ.</summary>
    public IReadOnlyList<OmfLibrary> Linked => _linked;

    /// <summary>Имена файлов подтверждённых библиотек (в порядке первого подтверждения).</summary>
    public IReadOnlyList<string> LinkedFileNames => _linked.Select(static l => l.FileName).ToList();

    /// <summary>
    /// Возвращает результаты сопоставления точки входа со всеми библиотеками (для диагностики/CLI).
    /// Не сужает кандидатов.
    /// </summary>
    public IReadOnlyList<EntryPointLibraryMatchInfo> GetEntryPointMatches(
        byte[] image,
        RelocationTable imageRelocations,
        int entryPointOffset,
        RegisterState initRegisters,
        string? symbolName = null,
        string? moduleName = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(imageRelocations);

        var results = new List<EntryPointLibraryMatchInfo>();

        foreach (var library in _allLibraries)
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

    /// <summary>
    /// Пытается разрешить адрес _main через crt0/__astart.
    /// Выполняет сопоставление точки входа, сужение кандидатов по crt0, поиск viable библиотек,
    /// выбор предпочтительной по приоритетам и подтверждение primary как astart-провайдера.
    /// При успехе возвращает true и заполняет <paramref name="result"/>.
    /// </summary>
    public bool TryResolveMain(
        byte[] image,
        RelocationTable imageRelocations,
        RegisterState initRegisters,
        int entryPoint,
        out MainResolution result)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(imageRelocations);

        result = default!;

        // 1. Сопоставляем точку входа (ищем crt0/__astart)
        var entryMatches = GetEntryPointMatches(
            image,
            imageRelocations,
            entryPoint,
            initRegisters,
            symbolName: AstartSymbol,
            moduleName: Crt0ModuleName);

        // 2. Сужаем кандидатов: оставляем только те, что имеют crt0-модуль и попали в entryMatches
        NarrowByEntryPointMatchesInternal(Crt0ModuleName, entryMatches);

        // 3. Находим все viable (astart + успешно разрешённый _main по FIXUPP)
        var viable = ResolveAllLibrariesAndMainsInternal(
            image, imageRelocations, entryMatches, initRegisters, entryPoint);

        if (viable.Count == 0)
        {
            return false;
        }

        // 4. Выбираем предпочтительный primary
        var (primaryLibrary, mainOffset, astartMatch) = PickPreferredLibraryInternal(viable);

        // 5. Подтверждаем primary как astart-провайдера (не удаляем другие viable crt0)
        ConfirmAstartProviderInternal(primaryLibrary);

        // 6. Строим возможные конфигурации (аналогично старому Decompiler)
        var possibleConfigurations = BuildPossibleConfigurations(viable, primaryLibrary);

        result = new MainResolution
        {
            PrimaryLibrary = primaryLibrary,
            MainOffset = mainOffset,
            AstartMatch = astartMatch,
            PossibleLibraryConfigurations = possibleConfigurations,
        };

        return true;
    }

    /// <summary>
    /// Пытается сопоставить процедуру по смещению с одной из текущих библиотек-кандидатов.
    /// При успехе подтверждает библиотеку, сужает кандидатов (удаляет другие с тем же символом)
    /// и возвращает информацию о совпадении.
    /// </summary>
    public LibraryMatchInfo? TryMatchProcedure(
        byte[] image,
        RelocationTable imageRelocations,
        int offset,
        RegisterState initRegisters)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(imageRelocations);

        var hits = new List<(OmfLibrary Library, LibraryMatchInfo Match)>();

        foreach (var library in _candidates)
        {
            var matches = LibraryFunctionMatcher.Match(
                image,
                imageRelocations,
                offset,
                library,
                initRegisters);

            foreach (var match in matches)
            {
                hits.Add((library, ToMatchInfo(match, library)));
            }
        }

        if (hits.Count == 0)
        {
            return null;
        }

        // Выбираем предпочтительный: сначала из уже подтверждённых (_linked, т.е. primary crt0 + ранее выбранные),
        // затем по алфавиту/порядку. Это гарантирует, что runtime-функции берутся из той же библиотеки, что и crt0 (SLIBCE а не SLIBC).
        var chosen = hits
            .OrderBy(h => _linked.Contains(h.Library) ? 0 : 1)
            .ThenBy(h => h.Library.FileName, StringComparer.OrdinalIgnoreCase)
            .First();

        var (chosenLibrary, chosenMatch) = chosen;

        // Подтверждаем и сужаем: другие .LIB с этим символом исключаются
        NarrowBySymbolInternal(chosenLibrary, chosenMatch.SymbolName);

        return chosenMatch;
    }

    // ==================== Внутренняя логика сужения и выбора (спрятана) ====================

    private void ConfirmLibraryInternal(OmfLibrary library)
    {
        if (!_linked.Contains(library))
        {
            _linked.Add(library);
        }
    }

    private void ConfirmAstartProviderInternal(OmfLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        ConfirmLibraryInternal(library);
        // Не удаляем другие с __astart — сохраняем как взаимозаменяемые crt0-базы.
    }

    private void NarrowBySymbolInternal(OmfLibrary matchedLibrary, string symbolName)
    {
        ArgumentNullException.ThrowIfNull(matchedLibrary);
        ArgumentException.ThrowIfNullOrEmpty(symbolName);

        ConfirmLibraryInternal(matchedLibrary);

        for (var i = _candidates.Count - 1; i >= 0; i--)
        {
            var library = _candidates[i];
            if (ReferenceEquals(library, matchedLibrary))
            {
                continue;
            }

            if (library.Symbols.ContainsKey(symbolName))
            {
                _candidates.RemoveAt(i);
            }
        }
    }

    private void NarrowByEntryPointMatchesInternal(string moduleName, IReadOnlyList<EntryPointLibraryMatchInfo> entryMatches)
    {
        ArgumentNullException.ThrowIfNull(entryMatches);

        if (entryMatches.Count == 0)
        {
            _candidates.Clear();
            return;
        }

        var allowed = entryMatches.Select(static m => m.Library).ToHashSet(ReferenceEqualityComparer.Instance);
        for (var i = _candidates.Count - 1; i >= 0; i--)
        {
            if (_candidates[i].Modules.Any(m => m.LibraryModuleName == moduleName) && !allowed.Contains(_candidates[i]))
            {
                _candidates.RemoveAt(i);
            }
        }
    }

    private List<(OmfLibrary Library, int MainOffset, LibraryMatchInfo AstartMatch)> ResolveAllLibrariesAndMainsInternal(
        byte[] image,
        RelocationTable imageRelocations,
        IReadOnlyList<EntryPointLibraryMatchInfo> entryMatches,
        RegisterState initRegisters,
        int entryPoint)
    {
        var candidateSet = _candidates.ToHashSet(ReferenceEqualityComparer.Instance);
        var result = new List<(OmfLibrary Library, int MainOffset, LibraryMatchInfo AstartMatch)>();

        foreach (var match in entryMatches)
        {
            if (match.AstartMatch is null)
            {
                continue;
            }

            if (!candidateSet.Contains(match.Library))
            {
                continue;
            }

            var astartOffset = ResolveAstartOffsetInternal(
                image, imageRelocations, match, entryPoint, initRegisters);

            var mainOffset = LibraryCallResolver.FindMainFromAstart(
                image,
                imageRelocations,
                match.Library,
                astartOffset,
                initRegisters,
                match.AstartMatch!.ModuleCodeOffset);

            result.Add((match.Library, mainOffset, match.AstartMatch));
        }

        return result;
    }

    private static (OmfLibrary Library, int MainOffset, LibraryMatchInfo AstartMatch) PickPreferredLibraryInternal(
        List<(OmfLibrary Library, int MainOffset, LibraryMatchInfo AstartMatch)> viable)
    {
        return viable
            .OrderBy(static v => MemoryModelLibraryPriority(v.Library.FileName))
            .ThenBy(static v => PreferEmulatorLibrary(v.Library.FileName))
            .ThenBy(static v => LibraryPriority(v.Library.FileName))
            .ThenBy(static v => v.Library.FileName, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private int ResolveAstartOffsetInternal(
        byte[] image,
        RelocationTable imageRelocations,
        EntryPointLibraryMatchInfo match,
        int entryPoint,
        RegisterState initRegisters)
    {
        if (match.AstartMatch is not null)
        {
            return entryPoint;
        }

        // Проверяем на точке входа
        var epMatches = LibraryFunctionMatcher.Match(
            image,
            imageRelocations,
            entryPoint,
            match.Library,
            initRegisters,
            symbolName: AstartSymbol,
            moduleName: Crt0ModuleName);

        if (epMatches.Count > 0)
        {
            return entryPoint;
        }

        // Fallback по всему образу
        return LibrarySymbolFinder.Find(image, imageRelocations, match.Library, AstartSymbol, initRegisters);
    }

    private List<LibraryConfiguration> BuildPossibleConfigurations(
        List<(OmfLibrary Library, int MainOffset, LibraryMatchInfo AstartMatch)> viable,
        OmfLibrary primaryLibrary)
    {
        var crtBases = viable.Select(v => v.Library).ToList();
        var addonFileNames = _linked
            .Where(lib => !crtBases.Any(baseLib => ReferenceEquals(baseLib, lib)))
            .Select(lib => lib.FileName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var orderedCrtBases = crtBases
            .OrderBy(static l => MemoryModelLibraryPriority(l.FileName))
            .ThenBy(static l => PreferEmulatorLibrary(l.FileName))
            .ThenBy(static l => LibraryPriority(l.FileName))
            .ThenBy(static l => l.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var configs = new List<LibraryConfiguration>();
        foreach (var crt in orderedCrtBases)
        {
            var names = new List<string> { crt.FileName };
            names.AddRange(addonFileNames);
            var ordered = names
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            configs.Add(new LibraryConfiguration
            {
                LibraryFileNames = ordered,
                PrimaryCrtLibrary = crt.FileName,
            });
        }

        if (configs.Count == 0)
        {
            configs.Add(new LibraryConfiguration
            {
                LibraryFileNames = LinkedFileNames,
                PrimaryCrtLibrary = primaryLibrary.FileName,
            });
        }

        return configs;
    }

    // ==================== Приоритеты выбора библиотек (публичны для диагностики и совместимости) ====================

    /// <summary>S → C → M → L: при нескольких совпадениях предпочитаем small-модель.</summary>
    public static int MemoryModelLibraryPriority(string fileName) =>
        MemoryModelDetector.DetectFromLibraryFileName(fileName) switch
        {
            MemoryModel.Small => 0,
            MemoryModel.Compact => 1,
            MemoryModel.Medium => 2,
            MemoryModel.Large => 3,
            _ => 4,
        };

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

    private static LibraryMatchInfo ToMatchInfo(LibraryMatchResult match, OmfLibrary library) =>
        new()
        {
            SymbolName = match.SymbolName,
            ModulePage = match.ModulePage,
            ModuleName = match.ModuleName,
            ModuleCodeOffset = match.ModuleCodeOffset,
            LibraryFileName = library.FileName,
        };
}

/// <summary>
/// Результат успешного разрешения main через LibraryProvider.
/// </summary>
public sealed record MainResolution
{
    public required OmfLibrary PrimaryLibrary { get; init; }
    public required int MainOffset { get; init; }
    public required LibraryMatchInfo AstartMatch { get; init; }
    public required IReadOnlyList<LibraryConfiguration> PossibleLibraryConfigurations { get; init; }
}

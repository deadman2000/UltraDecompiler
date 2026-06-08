using System.Text;
using LibParser.Models;
using UltraDecompiler.Headers;
using UltraDecompiler.LibMatching;
using UltraDecompiler.Parser;

namespace UltraDecompiler.Decompilation;

/// <summary>
/// Оркестратор декомпиляции: сопоставление с .LIB, рекурсивное дизассемблирование,
/// построение CFG/IR и сохранение C-файлов.
/// </summary>
public class Decompiler
{
    private const string MainFunction = "main";

    private readonly LibMatcher _libraryMatcher = new();

    /// <summary>
    /// Декомпилирует EXE/COM: находит <c>_main</c>, рекурсивно собирает функции,
    /// сопоставляет runtime с .LIB и сохраняет пользовательский код в <paramref name="outputDirectory"/>.
    /// </summary>
    public DecompileResult Decompile(
        string exePath,
        string libraryDirectory,
        string includeDirectory,
        string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        // Загружаем библиотеки
        var allLibraries = LibMatcher.LoadLibraries(libraryDirectory);
        if (allLibraries.Count == 0)
        {
            throw new DirectoryNotFoundException(
                $"В каталоге {libraryDirectory} не найдено файлов *.LIB.");
        }

        // Парсим исполняемый файл
        var parser = new DosExeParser(exePath);
        var initRegisters = parser.IsCom ? RegisterState.InitCom : RegisterState.InitExe;
        var entryPoint = (int)parser.EntryPointOffset;

        // Составляем множество подключаемых библиотек. По мере парсинга, будут выкидываться неподходящие
        var libraryCandidates = new LibraryCandidateSet(allLibraries);

        var entryMatches = _libraryMatcher.MatchEntryPoint(
            parser.Image,
            parser.RelocationTable,
            entryPoint,
            allLibraries,
            initRegisters,
            symbolName: LibMatcher.AstartSymbol,
            moduleName: LibMatcher.Crt0ModuleName);

        // Выкидываем библиотеки с Crt0ModuleName, но не в entryMatches
        libraryCandidates.NarrowByEntryPointMatches(LibMatcher.Crt0ModuleName, entryMatches);

        // Находим ВСЕ viable библиотеки, у которых есть __astart + успешное разрешение _main по FIXUPP.
        // Это позволяет сохранить все потенциальные crt0-варианты (взаимозаменяемые).
        var viable = ResolveAllLibrariesAndMains(
            parser,
            entryMatches,
            libraryCandidates.Candidates,
            initRegisters,
            entryPoint);
        if (viable.Count == 0)
        {
            return DecompileResult.Failed;
        }

        // Выбираем предпочтительный primary для фактической декомпиляции (по приоритетам модели памяти и т.д.)
        var (primaryLibrary, mainOffset, astartMatch) = PickPreferredLibrary(viable);

        // Подтверждаем primary как использованную (для linked), НО не удаляем другие библиотеки с __astart —
        // они остаются как альтернативные варианты crt0. Информация о вариантах будет собрана в конце.
        libraryCandidates.ConfirmAstartProvider(primaryLibrary);

        // Дизассемблируем main и все вложенные переходы
        var storage = CollectProcedures(
            parser,
            libraryCandidates,
            initRegisters,
            mainOffset);

        // Загружаем заголовки
        var headerCatalog = HeaderCatalog.Load(includeDirectory);

        // Подставляем функции
        ProcedureSignatureResolver.ResolveAll(storage, headerCatalog);

        // Экспортируем в C-файлы
        Directory.CreateDirectory(outputDirectory);
        var outputFiles = new List<string>();

        foreach (var procedure in storage.All
                     .Where(static p => !p.IsLibrary)
                     .OrderBy(static p => p.Offset))
        {
            var source = CCodeGenerator.GenerateProcedureC(parser, procedure, initRegisters, storage);
            var fileName = CCodeGenerator.FormatOutputFileName(procedure.Name, procedure.Offset);
            var filePath = Path.Combine(outputDirectory, fileName);
            File.WriteAllText(filePath, source, Encoding.UTF8);
            outputFiles.Add(filePath);
        }

        // Собираем возможные варианты подключения библиотек:
        // - базы: все viable crt0-библиотеки (взаимозаменяемые)
        // - аддоны: библиотеки, для которых во время сбора процедур был подтверждён хотя бы один символ
        var crtBases = viable.Select(v => v.Library).ToList();
        var addonFileNames = libraryCandidates.Linked
            .Where(lib => !crtBases.Any(baseLib => ReferenceEquals(baseLib, lib)))
            .Select(lib => lib.FileName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Формируем варианты в порядке приоритета (предпочтительный — первым)
        var orderedCrtBases = crtBases
            .OrderBy(static l => LibMatcher.MemoryModelLibraryPriority(l.FileName))
            .ThenBy(static l => LibMatcher.PreferEmulatorLibrary(l.FileName))
            .ThenBy(static l => LibMatcher.LibraryPriority(l.FileName))
            .ThenBy(static l => l.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var possibleConfigurations = new List<LibraryConfiguration>();
        foreach (var crt in orderedCrtBases)
        {
            var names = new List<string> { crt.FileName };
            names.AddRange(addonFileNames);
            var ordered = names
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
            possibleConfigurations.Add(new LibraryConfiguration
            {
                LibraryFileNames = ordered,
                PrimaryCrtLibrary = crt.FileName,
            });
        }

        // Если по какой-то причине не набралось — fallback на текущие linked
        if (possibleConfigurations.Count == 0)
        {
            possibleConfigurations.Add(new LibraryConfiguration
            {
                LibraryFileNames = libraryCandidates.LinkedFileNames,
                PrimaryCrtLibrary = primaryLibrary.FileName,
            });
        }

        // Для Linked публикуем чистый набор именно выбранного варианта (primary + его аддоны),
        // чтобы не "пачкать" список подтверждениями из альтернативных crt-библиотек при совпадении символов.
        var chosenConfig = possibleConfigurations.FirstOrDefault(
            c => string.Equals(c.PrimaryCrtLibrary, primaryLibrary.FileName, StringComparison.OrdinalIgnoreCase))
            ?? possibleConfigurations[0];

        return new DecompileResult
        {
            Success = true,
            MainOffset = mainOffset,
            LinkedLibraryFileNames = chosenConfig.LibraryFileNames,
            PossibleLibraryConfigurations = possibleConfigurations,
            Procedures = storage,
            OutputFiles = outputFiles,
        };
    }

    /// <summary>
    /// Находит ВСЕ библиотеки, для которых на точке входа есть __astart (crt0) и по метаданным
    /// FIXUPP библиотеки успешно резолвится адрес _main. Все такие — потенциальные кандидаты
    /// (взаимозаменяемые crt0). Не выбирает одну, возвращает список.
    /// </summary>
    private List<(OmfLibrary Library, int MainOffset, LibraryMatchInfo AstartMatch)> ResolveAllLibrariesAndMains(
        DosExeParser parser,
        IReadOnlyList<EntryPointLibraryMatchInfo> entryMatches,
        IReadOnlyList<OmfLibrary> candidateLibraries,
        RegisterState initRegisters,
        int entryPoint)
    {
        var candidateSet = candidateLibraries.ToHashSet(ReferenceEqualityComparer.Instance);
        var result = new List<(OmfLibrary Library, int MainOffset, LibraryMatchInfo AstartMatch)>();

        foreach (var match in entryMatches)
        {
            if (match.AstartMatch is null)
                continue;

            if (!candidateSet.Contains(match.Library))
                continue;

            try
            {
                var astartOffset = _libraryMatcher.ResolveAstartOffset(
                    parser.Image,
                    parser.RelocationTable,
                    match,
                    entryPoint,
                    initRegisters);
                var mainOffset = _libraryMatcher.FindMainOffset(
                    parser.Image,
                    parser.RelocationTable,
                    match.Library,
                    astartOffset,
                    initRegisters,
                    match.AstartMatch!.ModuleCodeOffset);

                result.Add((match.Library, mainOffset, match.AstartMatch));
            }
            catch (InvalidOperationException)
            {
                // Для этой библиотеки FIXUPP crt0 не соответствует образу — пропускаем (несовместима)
            }
        }

        return result;
    }

    /// <summary>
    /// Выбирает предпочтительную библиотеку из viable (по аналогии с приоритетами в decompile-main).
    /// Предпочтение: small-модель (S), наличие эмулятора (E), порядок LIBC и т.д.
    /// </summary>
    private static (OmfLibrary Library, int MainOffset, LibraryMatchInfo AstartMatch) PickPreferredLibrary(
        List<(OmfLibrary Library, int MainOffset, LibraryMatchInfo AstartMatch)> viable)
    {
        return viable
            .OrderBy(static v => LibMatcher.MemoryModelLibraryPriority(v.Library.FileName))
            .ThenBy(static v => LibMatcher.PreferEmulatorLibrary(v.Library.FileName))
            .ThenBy(static v => LibMatcher.LibraryPriority(v.Library.FileName))
            .ThenBy(static v => v.Library.FileName, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private ProcedureStorage CollectProcedures(
        DosExeParser parser,
        LibraryCandidateSet libraryCandidates,
        RegisterState initRegisters,
        int initOffset)
    {
        var storage = new ProcedureStorage();
        var pending = new Queue<int>();
        pending.Enqueue(initOffset);

        while (pending.Count > 0)
        {
            var offset = pending.Dequeue();

            // Проверяем, что процедура не была обработана
            if (storage.Contains(offset))
                continue;

            // Дизассемблируем процедуру
            var instructions = X86Disassembler.Disassemble(
                parser.Image,
                parser.RelocationTable,
                offset,
                initRegisters);

            if (instructions.Count == 0)
                continue;

            // Пробуем сматчить её с библиотечной функцией
            var libraryMatch = TryMatchLibrary(
                parser,
                offset,
                libraryCandidates,
                initRegisters);

            if (libraryMatch is not null)
            {
                // Если сматчили, регистрируем как библиотечную
                storage.Add(new DisassembledProcedure
                {
                    Offset = offset,
                    Instructions = instructions,
                    Name = LinkerSymbolNames.ToCName(libraryMatch.SymbolName),
                    IsLibrary = true,
                    LibraryMatch = libraryMatch,
                });
                continue;
            }

            // Регистрируем как пользовательскую. Для точки входа используем main
            var name = offset == initOffset
                ? MainFunction
                : $"sub_{offset:X4}";
            storage.Add(new DisassembledProcedure
            {
                Offset = offset,
                Instructions = instructions,
                Name = name,
                IsLibrary = false,
            });

            // Все переходы добавляем в очередь для обработки
            EnqueueExternalTargets(parser.Image, instructions, pending);
        }

        return storage;
    }

    /// <summary>
    /// Ищет совпадение среди кандидатов. При нахождении символа подтверждает библиотеку
    /// и сужает кандидатов (удаляет другие .LIB, экспортирующие тот же символ — они взаимозаменяемы
    /// для данного символа). Если символ найден в нескольких библиотеках — выбирается первая.
    /// </summary>
    private LibraryMatchInfo? TryMatchLibrary(
        DosExeParser parser,
        int offset,
        LibraryCandidateSet libraryCandidates,
        RegisterState initRegisters)
    {
        var hits = new List<(OmfLibrary Library, LibraryMatchInfo Match)>();

        foreach (var library in libraryCandidates.Candidates)
        {
            var matches = _libraryMatcher.MatchFunction(
                parser.Image,
                parser.RelocationTable,
                offset,
                library,
                initRegisters);

            foreach (var match in matches)
            {
                hits.Add((library, match));
            }
        }

        if (hits.Count == 0)
        {
            return null;
        }

        // Выбираем первое совпадение (можно улучшить приоритетами).
        var (chosenLibrary, chosenMatch) = hits[0];

        // Подтверждаем и сужаем: другие библиотеки с этим же символом исключаются из кандидатов
        // (это обеспечивает, что для одного символа выбирается одна "семейство" библиотек).
        // Для crt0-вариантов сужение по __astart не делается на верхнем уровне — варианты сохраняются.
        libraryCandidates.NarrowBySymbol(chosenLibrary, chosenMatch.SymbolName);

        return chosenMatch;
    }

    /// <summary>
    /// Добавляет все переходы процедуры в очередь для обработки
    /// </summary>
    private static void EnqueueExternalTargets(byte[] image, IReadOnlyList<Instruction> instructions, Queue<int> pending)
    {
        var functionOffsets = instructions.Select(static i => i.Offset).Distinct();

        foreach (var instr in instructions)
        {
            if (!instr.IsCall && !instr.IsUnconditionalJump)
            {
                continue;
            }

            var target = instr.GetEffectiveJumpTarget(image);
            if (target >= 0 && !functionOffsets.Contains(target))
            {
                pending.Enqueue(target);
            }
        }
    }

}

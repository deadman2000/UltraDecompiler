using System.Text;
using LibParser.Models;
using LibParser.Omf;
using UltraDecompiler.Graph;
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
    private const string AstartSymbol = "__astart";
    private const string Crt0ModuleName = "crt0";
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
        var allLibraries = LoadLibraries(libraryDirectory);
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
            symbolName: null,
            moduleName: Crt0ModuleName);

        // Выкидываем библиотеки с Crt0ModuleName, но не в entryMatches
        libraryCandidates.NarrowByEntryPointMatches(Crt0ModuleName, entryMatches);

        var resolved = ResolveLibraryAndMain(
            parser,
            entryMatches,
            libraryCandidates.Candidates,
            initRegisters,
            entryPoint);
        if (resolved is null)
        {
            return DecompileResult.Failed;
        }

        // Выкидываем библиотеки с __astart, кроме primaryLibrary
        var (primaryLibrary, mainOffset, astartMatch) = resolved.Value;
        libraryCandidates.NarrowBySymbol(primaryLibrary, AstartSymbol);

        // Дизассемблируем main и все вложенные переходы
        var storage = CollectProcedures(
            parser,
            libraryCandidates,
            primaryLibrary,
            initRegisters,
            mainOffset);

        var headerCatalog = HeaderCatalog.Load(includeDirectory);
        ProcedureSignatureResolver.ResolveAll(storage, headerCatalog);

        Directory.CreateDirectory(outputDirectory);
        var outputFiles = new List<string>();

        foreach (var procedure in storage.All
                     .Where(static p => !p.IsLibrary)
                     .OrderBy(static p => p.Offset))
        {
            var source = DecompileProcedureToC(parser, procedure, initRegisters, storage);
            var fileName = FormatOutputFileName(procedure.Name, procedure.Offset);
            var filePath = Path.Combine(outputDirectory, fileName);
            File.WriteAllText(filePath, source, Encoding.UTF8);
            outputFiles.Add(filePath);
        }

        return new DecompileResult
        {
            Success = true,
            MainOffset = mainOffset,
            LinkedLibraryFileNames = libraryCandidates.LinkedFileNames,
            Procedures = storage,
            OutputFiles = outputFiles,
        };
    }

    private static List<OmfLibrary> LoadLibraries(string libraryDirectory)
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

    private (OmfLibrary Library, int MainOffset, LibraryMatchInfo AstartMatch)? ResolveLibraryAndMain(
        DosExeParser parser,
        IReadOnlyList<EntryPointLibraryMatchInfo> entryMatches,
        IReadOnlyList<OmfLibrary> candidateLibraries,
        RegisterState initRegisters,
        int entryPoint)
    {
        var candidateSet = candidateLibraries.ToHashSet();

        foreach (var match in OrderLibraryCandidates(entryMatches))
        {
            if (!candidateSet.Contains(match.Library))
            {
                continue;
            }

            var astartOffset = ResolveAstartOffset(match, entryPoint);
            var mainOffset = _libraryMatcher.FindMainOffset(
                parser.Image,
                parser.RelocationTable,
                match.Library,
                astartOffset,
                initRegisters,
                match.AstartMatch!.ModuleCodeOffset);

            return (match.Library, mainOffset, match.AstartMatch);
        }

        return null;
    }

    private static IEnumerable<EntryPointLibraryMatchInfo> OrderLibraryCandidates(
        IReadOnlyList<EntryPointLibraryMatchInfo> entryMatches) =>
        entryMatches
            .Where(static m => m.AstartMatch is not null)
            .OrderBy(static m => LibraryPriority(m.Library.FileName))
            .ThenBy(static m => PreferEmulatorLibrary(m.Library.FileName))
            .ThenBy(static m => MemoryModelLibraryPriority(m.Library.FileName))
            .ThenBy(static m => m.Library.FileName, StringComparer.OrdinalIgnoreCase);

    private ProcedureStorage CollectProcedures(
        DosExeParser parser,
        LibraryCandidateSet libraryCandidates,
        OmfLibrary primaryLibrary,
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
                primaryLibrary,
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
    /// Ищет совпадение среди кандидатов; при однозначном символе сужает набор библиотек.
    /// </summary>
    private LibraryMatchInfo? TryMatchLibrary(
        DosExeParser parser,
        int offset,
        LibraryCandidateSet libraryCandidates,
        OmfLibrary primaryLibrary,
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

            if (matches.Count > 0)
            {
                hits.Add((library, matches[0]));
            }
        }

        if (hits.Count == 0)
        {
            return null;
        }

        var (chosenLibrary, chosenMatch) = ChooseBestHit(hits, libraryCandidates.Linked, primaryLibrary);

        var librariesWithSameSymbol = hits
            .Where(h => h.Match.SymbolName == chosenMatch.SymbolName)
            .Select(static h => h.Library)
            .ToList();

        if (librariesWithSameSymbol.Count == 1)
        {
            libraryCandidates.NarrowBySymbol(chosenLibrary, chosenMatch.SymbolName);
        }
        else
        {
            libraryCandidates.ConfirmLibrary(chosenLibrary);
        }

        return chosenMatch;
    }

    private static (OmfLibrary Library, LibraryMatchInfo Match) ChooseBestHit(
        IReadOnlyList<(OmfLibrary Library, LibraryMatchInfo Match)> hits,
        IReadOnlyList<OmfLibrary> linkedLibraries,
        OmfLibrary primaryLibrary)
    {
        var linked = hits.Where(h => linkedLibraries.Contains(h.Library)).ToList();
        if (linked.Count == 1)
        {
            return linked[0];
        }

        if (linked.Count > 1)
        {
            return OrderHits(linked).First();
        }

        var primaryHits = hits.Where(h => ReferenceEquals(h.Library, primaryLibrary)).ToList();
        if (primaryHits.Count == 1)
        {
            return primaryHits[0];
        }

        if (primaryHits.Count > 1)
        {
            return OrderHits(primaryHits).First();
        }

        return OrderHits(hits).First();
    }

    private static IEnumerable<(OmfLibrary Library, LibraryMatchInfo Match)> OrderHits(
        IEnumerable<(OmfLibrary Library, LibraryMatchInfo Match)> hits) =>
        hits
            .OrderBy(static h => LibraryPriority(h.Library.FileName))
            .ThenBy(static h => PreferEmulatorLibrary(h.Library.FileName))
            .ThenBy(static h => MemoryModelLibraryPriority(h.Library.FileName))
            .ThenBy(static h => h.Library.FileName, StringComparer.OrdinalIgnoreCase);

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

    private static int ResolveAstartOffset(
        EntryPointLibraryMatchInfo match,
        int entryPoint)
    {
        if (match.Matches.Any(static m => m.SymbolName == AstartSymbol))
        {
            return entryPoint;
        }

        throw new InvalidOperationException(
            $"Библиотека {match.Library.FileName} не содержит {AstartSymbol} на точке входа.");
    }

    private static string DecompileProcedureToC(
        DosExeParser parser,
        DisassembledProcedure procedure,
        RegisterState initRegisters,
        ProcedureStorage procedures)
    {
        var cfg = new ControlFlowGraph();
        cfg.BuildFromInstructions(procedure.Instructions, procedure.Offset, parser.Image, initRegisters);

        var expressions = new ExpressionBuilder();
        expressions.Build(cfg, parser.IsCom, procedures);

        var operations = expressions.GetAllOperations();
        return FormatCFunction(procedure, operations);
    }

    private static string FormatCFunction(DisassembledProcedure procedure, IReadOnlyList<Operation> operations)
    {
        var sb = new StringBuilder();
        var returnType = procedure.Signature.ReturnType.ToString();
        var parameters = FormatParameterList(procedure.Signature);
        sb.AppendLine($"{returnType} {procedure.Name}({parameters})");
        sb.AppendLine("{");

        if (operations.Count == 0)
        {
            sb.AppendLine("    ;");
        }
        else
        {
            foreach (var operation in operations)
            {
                operation.AppendToCString(sb, indent: 1, asStatement: true);
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string FormatParameterList(ProcedureSignature signature)
    {
        if (signature.Parameters.Count == 0)
        {
            return "void";
        }

        return string.Join(", ", signature.Parameters.Select(static (p, i) => $"{p.Type} arg{i}"));
    }

    private static string FormatOutputFileName(string name, int offset)
    {
        if (name.StartsWith('_') || char.IsLetter(name[0]))
        {
            return $"{name}.c";
        }

        return $"{name}_{offset:X4}.c";
    }

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
}

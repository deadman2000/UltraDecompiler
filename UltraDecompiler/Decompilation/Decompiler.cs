using System.Text;
using LibParser.Models;
using LibParser.Omf;
using UltraDecompiler.Graph;
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
    private const string MainSymbol = "_main";

    private readonly LibMatcher _libraryMatcher = new();

    /// <summary>
    /// Декомпилирует EXE/COM: находит <c>_main</c>, рекурсивно собирает функции,
    /// сопоставляет runtime с .LIB и сохраняет пользовательский код в <paramref name="outputDirectory"/>.
    /// </summary>
    public DecompileResult Decompile(string exePath, string libraryDirectory, string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var libraries = LoadLibraries(libraryDirectory);
        if (libraries.Count == 0)
        {
            throw new DirectoryNotFoundException(
                $"В каталоге {libraryDirectory} не найдено файлов *.LIB.");
        }

        var parser = new DosExeParser(exePath);
        var initRegisters = parser.IsCom ? RegisterState.InitCom : RegisterState.InitExe;
        var entryPoint = (int)parser.EntryPointOffset;

        var entryMatches = _libraryMatcher.MatchEntryPoint(
            parser.Image,
            parser.RelocationTable,
            entryPoint,
            libraries,
            initRegisters,
            symbolName: null,
            moduleName: Crt0ModuleName);

        var resolved = ResolveLibraryAndMain(parser, entryMatches, initRegisters, entryPoint);
        if (resolved is null)
        {
            return DecompileResult.Failed;
        }

        var (selectedLibrary, mainOffset, astartMatch) = resolved.Value;
        var storage = CollectProcedures(
            parser,
            libraries,
            initRegisters,
            mainOffset);

        Directory.CreateDirectory(outputDirectory);
        var knownProcedures = storage.All.ToDictionary(static p => p.Offset, static p => p.Name);
        var outputFiles = new List<string>();

        foreach (var procedure in storage.All
                     .Where(static p => !p.IsLibrary)
                     .OrderBy(static p => p.Offset))
        {
            try
            {
                var source = DecompileProcedureToC(parser, procedure, initRegisters, knownProcedures);
                var fileName = FormatOutputFileName(procedure.Name, procedure.Offset);
                var filePath = Path.Combine(outputDirectory, fileName);
                File.WriteAllText(filePath, source, Encoding.UTF8);
                outputFiles.Add(filePath);
            }
            catch (InvalidOperationException)
            {
            }
            catch (NotImplementedException)
            {
            }
        }

        return new DecompileResult
        {
            Success = true,
            MainOffset = mainOffset,
            SelectedLibraryFileName = selectedLibrary.FileName,
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
        RegisterState initRegisters,
        int entryPoint)
    {
        foreach (var match in OrderLibraryCandidates(entryMatches))
        {
            try
            {
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
            catch (InvalidOperationException)
            {
            }
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
        List<OmfLibrary> libraries,
        RegisterState initRegisters,
        int mainOffset)
    {
        var storage = new ProcedureStorage();
        var pending = new Queue<int>();
        pending.Enqueue(mainOffset);

        while (pending.Count > 0)
        {
            var offset = pending.Dequeue();
            if (storage.Contains(offset))
            {
                continue;
            }

            var instructions = X86Disassembler.Disassemble(
                parser.Image,
                parser.RelocationTable,
                offset,
                initRegisters);

            if (instructions.Count == 0)
            {
                continue;
            }

            var libraryMatch = TryMatchLibrary(
                parser,
                offset,
                libraries,
                initRegisters);

            if (libraryMatch is not null)
            {
                storage.Add(new DisassembledProcedure
                {
                    Offset = offset,
                    Instructions = instructions,
                    Name = libraryMatch.Value.Match.SymbolName,
                    IsLibrary = true,
                    LibraryMatch = libraryMatch.Value.Match,
                });

                RejectLibrariesWithDuplicateSymbol(
                    libraryMatch.Value.Library,
                    libraryMatch.Value.Match.SymbolName,
                    libraries);
                continue;
            }

            var name = offset == mainOffset ? MainSymbol : $"sub_{offset:X4}";
            storage.Add(new DisassembledProcedure
            {
                Offset = offset,
                Instructions = instructions,
                Name = name,
                IsLibrary = false,
            });

            EnqueueExternalTargets(parser.Image, instructions, pending);
        }

        return storage;
    }

    private LibraryProcedureMatch? TryMatchLibrary(
        DosExeParser parser,
        int offset,
        IReadOnlyList<OmfLibrary> libraries,
        RegisterState initRegisters)
    {
        var candidates = new List<LibraryProcedureMatch>();

        foreach (var library in libraries)
        {
            var matches = _libraryMatcher.MatchFunction(
                parser.Image,
                parser.RelocationTable,
                offset,
                library,
                initRegisters);

            if (matches.Count == 0)
            {
                continue;
            }

            candidates.Add(new LibraryProcedureMatch(library, matches[0]));
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[0];
    }

    /// <summary>
    /// Исключает библиотеки с тем же именем символа: после совпадения в одной .LIB
    /// остальные кандидаты с дубликатом символа в словаре больше не участвуют в поиске.
    /// </summary>
    private static void RejectLibrariesWithDuplicateSymbol(
        OmfLibrary matchedLibrary,
        string symbolName,
        List<OmfLibrary> libraries)
    {
        for (int i = 0; i < libraries.Count; i++)
        {
            var library = libraries[i];
            if (ReferenceEquals(library, matchedLibrary))
            {
                continue;
            }

            if (library.Symbols.ContainsKey(symbolName))
            {
                libraries.RemoveAt(i);
                i--;
            }
        }
    }

    private static void EnqueueExternalTargets(byte[] image, IReadOnlyList<Instruction> instructions, Queue<int> pending)
    {
        var functionOffsets = instructions.Select(static i => i.Offset).ToHashSet();

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
        IReadOnlyDictionary<int, string> knownProcedures)
    {
        var cfg = new ControlFlowGraph();
        cfg.BuildFromInstructions(procedure.Instructions, procedure.Offset, parser.Image, initRegisters);

        var expressions = new ExpressionBuilder();
        expressions.Build(cfg, parser.IsCom, knownProcedures);

        var operations = expressions.GetAllOperations();
        return FormatCFunction(procedure.Name, operations);
    }

    private static string FormatCFunction(string name, IReadOnlyList<Operation> operations)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"void {name}(void)");
        sb.AppendLine("{");

        if (operations.Count == 0)
        {
            sb.AppendLine("    ;");
        }
        else
        {
            foreach (var operation in operations)
            {
                sb.AppendLine($"    {operation.ToCString(asStatement: true)}");
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
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

    private readonly record struct LibraryProcedureMatch(OmfLibrary Library, LibraryMatchInfo Match);
}

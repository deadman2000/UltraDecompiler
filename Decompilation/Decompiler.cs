using System.Diagnostics;
using System.Text;
using UltraDecompiler.CodeGeneration;
using UltraDecompiler.Common;
using UltraDecompiler.Decompilation.Heuristics;
using UltraDecompiler.Disassembly.Parser;
using UltraDecompiler.Ir.Builder.Loops;
using UltraDecompiler.Ir.Helpers;
using UltraDecompiler.LibMatching;
using UltraDecompiler.PostProcessing.Abstractions;
using UltraDecompiler.PostProcessing.Literals;
using UltraDecompiler.PostProcessing.Profiles;
using UltraDecompiler.PostProcessing.Stack;
using UltraDecompiler.PostProcessing.Types;

namespace UltraDecompiler.Decompilation;

/// <summary>
/// Оркестратор декомпиляции: сопоставление с .LIB, рекурсивное дизассемблирование,
/// построение CFG/IR и сохранение C-файлов.
/// </summary>
public class Decompiler
{
    private const string MainFunction = "main";

    private readonly string _exePath;
    private readonly string _libraryDirectory;
    private readonly string _includeDirectory;
    private readonly string? _outputDirectory;
    private readonly IReadOnlyList<string>? _libraryFileNames;

    // Состояние декомпиляции
    private DosExeParser _parser = null!;
    private LibraryProvider _provider = null!;
    private MainResolution _resolution = default!;
    private RegisterState _initRegisters;
    private int _mainOffset;
    private Dictionary<int, (IReadOnlyList<Instruction> Instructions, bool IsLibrary, string? Name)> _instructionsMap = null!;
    private MemoryModel _memoryModel;
    private OptimizationLevel _optimizationLevel;
    private ProcedureStorage _storage = null!;
    private HeaderCatalog _headerCatalog = null!;
    private ExeImageLayout _imageLayout = default!;
    private CompilerOptions _compilerOptions = default!;
    private GlobalVariableRegistry _globalRegistry = null!;
    private List<(DisassembledProcedure Procedure, IReadOnlyList<Operation> Operations)> _preparedProcedures = null!;

    public OptimizationLevel OptimizationLevel => _optimizationLevel;

    public MemoryModel MemoryModel => _memoryModel;

    public ProcedureStorage Procedures => _storage;

    public Decompiler(
        string exePath,
        string libraryDirectory,
        string includeDirectory,
        string? outputDirectory,
        IReadOnlyList<string>? libraryFileNames = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(includeDirectory);

        _exePath = exePath;
        _libraryDirectory = libraryDirectory;
        _includeDirectory = includeDirectory;
        _outputDirectory = outputDirectory;
        _libraryFileNames = libraryFileNames;
    }

    /// <summary>
    /// Декомпилирует EXE/COM: находит <c>_main</c>, рекурсивно собирает функции,
    /// сопоставляет runtime с .LIB и сохраняет пользовательский код в <paramref name="outputDirectory"/>.
    /// </summary>
    /// <param name="exportGraph">Экспортировать CFG в .dot/.svg файлы.</param>
    /// <returns>Результат декомпиляции.</returns>
    public DecompileResult Decompile(bool exportGraph = false)
    {
        // Загрузка образа и поиск точки входа
        if (!LoadImageAndResolveMain())
            return DecompileResult.Failed;

        // Детекция модели памяти
        DetectMemoryModel();

        // Детекция уровня оптимизации
        DetectOptimizaionLevel();

        // Построение IR для всех процедур
        BuildIrForAllProcedures();

        // Экспорт графов потока управления (опционально)
        if (exportGraph)
        {
            ExportControlFlowGraphs();
        }

        // Загрузка заголовков и разрешение сигнатур
        LoadHeadersAndResolveSignatures();

        // Подготовка опций компилятора
        PrepareCompilerOptions();

        // Post-processing IR
        ApplyPostProcessing();

        // Материализация глобальных переменных
        MaterializeGlobalVariables();

        var chosenConfig = SelectLibraryConfiguration();
        var outputFiles = new List<string>();
        if (!string.IsNullOrEmpty(_outputDirectory))
        {
            // Генерация заголовочных файлов
            Directory.CreateDirectory(_outputDirectory);

            GenerateHeaderFiles(ref outputFiles);

            // Генерация C-исходников
            var makefileSourceFileNames = GenerateCSourceFiles(ref outputFiles);

            // Генерация Makefile
            var makefilePath = GenerateMakefile(chosenConfig, makefileSourceFileNames, _compilerOptions);
            outputFiles.Add(makefilePath);
        }

        return new DecompileResult
        {
            Success = true,
            MainOffset = _mainOffset,
            LinkedLibraryFileNames = chosenConfig.LibraryFileNames,
            PossibleLibraryConfigurations = _resolution.PossibleLibraryConfigurations,
            Procedures = _storage,
            OutputFiles = outputFiles,
            CompilerOptions = _compilerOptions,
        };
    }

    private void PrepareInstructionsMap()
    {
        // Сбор инструкций всех процедур (без IR)
        _instructionsMap ??= CollectInstructionsForAllProcedures();
    }

    public void DetectOptimizaionLevel()
    {
        PrepareInstructionsMap();
        _optimizationLevel = OptimizationLevelHeuristics.DetectFromInstructionsMap(_instructionsMap);
    }

    /// <summary>
    /// Загружает образ EXE/COM и находит точку входа _main через .LIB.
    /// </summary>
    /// <returns>true если успешно, false если не удалось найти _main.</returns>
    public bool LoadImageAndResolveMain()
    {
        _parser = new DosExeParser(_exePath);
        _initRegisters = _parser.IsCom ? RegisterState.InitCom : RegisterState.InitExe;
        var entryPoint = (int)_parser.EntryPointOffset;

        _provider = new LibraryProvider(_libraryDirectory, _libraryFileNames);

        if (!_provider.TryResolveMain(
                _parser.Image,
                _parser.RelocationTable,
                _initRegisters,
                entryPoint,
                out _resolution))
        {
            return false;
        }

        _mainOffset = _resolution.MainOffset;
        _imageLayout = ExeImageLayout.From(_parser);
        return true;
    }

    /// <summary>
    /// Собирает только инструкции всех процедур (без IR) для детекции оптимизации.
    /// </summary>
    private Dictionary<int, (IReadOnlyList<Instruction> Instructions, bool IsLibrary, string? Name)> CollectInstructionsForAllProcedures()
    {
        var instructionsMap = new Dictionary<int, (IReadOnlyList<Instruction>, bool, string?)>();
        var pending = new Queue<int>();
        pending.Enqueue(_mainOffset);

        while (pending.Count > 0)
        {
            var offset = pending.Dequeue();

            if (instructionsMap.ContainsKey(offset))
                continue;

            var instructions = X86Disassembler.Disassemble(
                _parser.Image,
                _parser.RelocationTable,
                offset,
                _initRegisters);

            if (instructions.Count == 0)
                continue;

            // Проверяем library match
            var libraryMatch = _provider.TryMatchProcedure(
                _parser.Image,
                _parser.RelocationTable,
                offset,
                _initRegisters);

            if (libraryMatch is not null)
            {
                instructionsMap[offset] = (
                    instructions,
                    true,
                    LinkerSymbolNames.ToCName(libraryMatch.SymbolName));
                continue;
            }

            var name = offset == _mainOffset ? MainFunction : $"sub_{offset:X4}";
            instructionsMap[offset] = (instructions, false, name);

            EnqueueExternalTargets(instructions, pending);
        }

        return instructionsMap;
    }

    /// <summary>
    /// Строит IR для всех процедур на основе заранее собранных инструкций.
    /// </summary>
    private void BuildIrForAllProcedures()
    {
        PrepareInstructionsMap();
        _storage = new ProcedureStorage();

        foreach (var (offset, (instructions, isLibrary, name)) in _instructionsMap)
        {
            if (isLibrary)
            {
                var libraryMatch = _provider.TryMatchProcedure(
                    _parser.Image,
                    _parser.RelocationTable,
                    offset,
                    _initRegisters);

                if (libraryMatch is not null)
                {
                    var libraryProc = new DisassembledProcedure
                    {
                        Offset = offset,
                        Instructions = instructions,
                        Name = name!,
                        IsLibrary = true,
                        LibraryMatch = libraryMatch,
                    };
                    _storage.Add(libraryProc);
                }
                continue;
            }

            var cfg = new ControlFlowGraph();
            cfg.BuildFromInstructions(instructions, offset, _initRegisters);

            var expressions = ExpressionBuilder.Create(cfg, _optimizationLevel);
            expressions.Build();
            expressions.Optimize();

            var userProc = new DisassembledProcedure
            {
                Offset = offset,
                Instructions = instructions,
                Expressions = expressions,
                Name = name!,
                IsLibrary = false,
                Graph = cfg,
            };

            _storage.Add(userProc);
        }
    }

    /// <summary>
    /// Экспортирует CFG пользовательских процедур в .dot и .svg (для отладки).
    /// </summary>
    private void ExportControlFlowGraphs()
    {
        if (_outputDirectory == null)
            return;

        foreach (var proc in _storage.All)
        {
            if (proc.IsLibrary || proc.Expressions == null)
                continue;

            var exprDotPath = Path.Combine(_outputDirectory, $"{proc.Name}.dot");
            var exprSvgPath = Path.Combine(_outputDirectory, $"{proc.Name}.svg");
            proc.Expressions.SaveDot(exprDotPath);
            ConvertDotToSvg(exprDotPath, exprSvgPath);
        }
    }

    /// <summary>
    /// Загружает заголовки, разрешает сигнатуры процедур и вызовы.
    /// </summary>
    private void LoadHeadersAndResolveSignatures()
    {
        _headerCatalog = HeaderCatalog.Load(_includeDirectory);

        // Подставляем функции (сигнатуры из заголовков .LIB или анализ тел пользовательских процедур).
        ProcedureSignatureResolver.ResolveAll(_storage, _headerCatalog);

        // Подставляем в CallExpr (через CallState) имена (библиотечные) и аргументы по сигнатурам callee.
        // Для аргументов char* (из заголовков) near-указатель переводится в StringExpr по адресу в образе.
        CallSiteResolver.ResolveAll(_storage, _parser.Image, _imageLayout);

        // Safety net после разрешения: гарантируем StringExpr для char* аргументов по типу из заголовка,
        // даже если CallExpr в IR не имел CallState на момент финального прохода.
        MaterializeCharPtrLiterals();
    }

    public void DetectMemoryModel()
    {
        _memoryModel = MemoryModelDetector.DetectFromLibraryFileName(_resolution.PrimaryLibrary.FileName);
    }

    /// <summary>
    /// Подготавливает опции компилятора на основе разрешённой библиотеки и анализа IR.
    /// </summary>
    private void PrepareCompilerOptions()
    {
        _compilerOptions = new CompilerOptions
        {
            MemoryModel = _memoryModel,
            StackCheckingEnabled = StackCheckDetector.Analyze(_storage),
            OptimizationLevel = _optimizationLevel,
        };
    }

    /// <summary>
    /// Применяет post-processing passes ко всем пользовательским процедурам.
    /// </summary>
    private void ApplyPostProcessing()
    {
        var profile = DecompilationProfileRegistry.GetProfile(_compilerOptions.OptimizationLevel);

        var userProcedures = _storage.All
            .Where(static p => !p.IsLibrary)
            .OrderBy(static p => p.Offset)
            .ToList();

        _preparedProcedures = new List<(DisassembledProcedure, IReadOnlyList<Operation>)>();

        foreach (var procedure in userProcedures)
        {
            var postCtx = new PostProcessContext
            {
                Procedure = procedure,
                Storage = _storage,
                HeaderCatalog = _headerCatalog,
                Image = _parser.Image,
                Layout = _imageLayout,
                CompilerOptions = _compilerOptions,
            };

            var flattener = new OperationFlattener(procedure.Expressions!, procedure.Graph!.Blocks, LoopAnalyzerFactory.Create(_optimizationLevel));
            var operations = flattener.GetAllOperations();

            foreach (var pass in profile.GetProcedurePasses())
            {
                operations = pass.Apply(postCtx, operations);
                if (pass.Name == nameof(MainParameterNormalizer))
                {
                    procedure.Callees = ProcedureDependencyCollector.Collect(operations);
                }
            }

            _preparedProcedures.Add((procedure, operations));
        }
    }

    /// <summary>
    /// Материализует глобальные переменные в операциях всех процедур.
    /// </summary>
    private void MaterializeGlobalVariables()
    {
        _globalRegistry = new GlobalVariableRegistry();
        for (var i = 0; i < _preparedProcedures.Count; i++)
        {
            var (procedure, operations) = _preparedProcedures[i];
            _preparedProcedures[i] = (
                procedure,
                GlobalVariableMaterializer.Materialize(operations, _globalRegistry, _parser.Image, _imageLayout));
        }
    }

    /// <summary>
    /// Генерирует заголовочные .h файлы для пользовательских процедур (если процедура одна).
    /// </summary>
    private void GenerateHeaderFiles(ref List<string> outputFiles)
    {
        if (_preparedProcedures.Count != 1)
            return;

        var userProcedures = _storage.All.Where(static p => !p.IsLibrary).ToList();
        var referencedUserProcedures = ProcedureDependencyCollector.CollectReferencedUserProcedureNames(userProcedures, _storage);

        foreach (var procedure in userProcedures.Where(p => referencedUserProcedures.Contains(p.Name)))
        {
            var headerSource = CCodeGenerator.FormatHeaderFile(procedure.ToCodegenModel());
            var headerFileName = CCodeGenerator.FormatHeaderFileName(procedure.Name, procedure.Offset);
            var headerPath = Path.Combine(_outputDirectory, headerFileName);
            File.WriteAllText(headerPath, headerSource, Encoding.ASCII);
            outputFiles.Add(headerPath);
        }
    }

    /// <summary>
    /// Генерирует C-исходники (combined или раздельные .c файлы).
    /// </summary>
    /// <returns>Список имён сгенерированных файлов для Makefile.</returns>
    private List<string> GenerateCSourceFiles(ref List<string> outputFiles)
    {
        var combinedUnits = new List<(ProcedureCodegenModel Procedure, IReadOnlyList<Operation> Operations, IReadOnlyList<string> Includes)>();

        foreach (var (procedure, operations) in _preparedProcedures)
        {
            var includes = ProcedureIncludeResolver.ResolveIncludes(
                procedure,
                procedure.Callees,
                _storage,
                _headerCatalog);

            combinedUnits.Add((procedure.ToCodegenModel(), operations, includes));
        }

        if (_preparedProcedures.Count > 1)
        {
            // Множество процедур — объединяем в один .c файл
            var combinedFileName = CCodeGenerator.FormatCombinedSourceFileName(Path.GetFileName(_exePath));
            var combinedSource = CCodeGenerator.FormatCombinedCSource(combinedUnits, _globalRegistry.All);
            var combinedPath = Path.Combine(_outputDirectory, combinedFileName);
            File.WriteAllText(combinedPath, combinedSource, Encoding.ASCII);
            outputFiles.Add(combinedPath);
            return [combinedFileName];
        }
        else
        {
            // Одна процедура — отдельный .c файл
            var (procedure, operations) = _preparedProcedures[0];
            var includes = combinedUnits[0].Includes;
            var source = CCodeGenerator.FormatCSource(
                procedure.ToCodegenModel(),
                operations,
                includes,
                _globalRegistry.All);
            var fileName = CCodeGenerator.FormatOutputFileName(procedure.Name, procedure.Offset);
            var filePath = Path.Combine(_outputDirectory, fileName);
            File.WriteAllText(filePath, source, Encoding.ASCII);
            outputFiles.Add(filePath);
            return [fileName];
        }
    }

    /// <summary>
    /// Выбирает конфигурацию библиотек, соответствующую primary (или первую доступную).
    /// </summary>
    private LibraryConfiguration SelectLibraryConfiguration()
    {
        return _resolution.PossibleLibraryConfigurations.FirstOrDefault(
            c => string.Equals(c.PrimaryCrtLibrary, _resolution.PrimaryLibrary.FileName, StringComparison.OrdinalIgnoreCase))
            ?? _resolution.PossibleLibraryConfigurations[0];
    }

    /// <summary>
    /// Генерирует Makefile для сборки скомпилированного кода.
    /// </summary>
    private string GenerateMakefile(
        LibraryConfiguration config,
        List<string> sourceFileNames,
        CompilerOptions compilerOptions)
    {
        return MakefileGenerator.WriteMakefile(
            new MakefileOptions
            {
                TargetExeFileName = Path.GetFileName(_exePath),
                SourceFileNames = sourceFileNames,
                CompilerOptions = compilerOptions,
                LibraryFileNames = config.LibraryFileNames,
                OutputDirectory = Path.GetFullPath(_outputDirectory),
            },
            _outputDirectory);
    }

    /// <summary>
    /// Ставим в очередь внешние цели вызовов и переходов для рекурсивного разбора.
    /// </summary>
    private static void EnqueueExternalTargets(IReadOnlyList<Instruction> instructions, Queue<int> pending)
    {
        var functionOffsets = instructions.Select(static i => i.Offset).Distinct();

        foreach (var instr in instructions)
        {
            if (!instr.IsCall && !instr.IsUnconditionalJump)
                continue;

            var target = instr.JumpTarget;
            if (target >= 0 && !functionOffsets.Contains(target))
                pending.Enqueue(target);
        }
    }

    /// <summary>
    /// Ищем по всем процедурам и для всех CallExpr near-аргументы char*,
    /// превращаем ConstExpr/ImageOffsetExpr в StringExpr если они указывают на строковый литерал.
    /// </summary>
    private void MaterializeCharPtrLiterals()
    {
        if (_parser.Image.Length == 0) return;

        foreach (var proc in _storage.All)
        {
            if (proc.Expressions == null)
                continue;

            foreach (var block in proc.Expressions.Blocks)
            {
                for (int i = 0; i < block.Operations.Count; i++)
                {
                    if (block.Operations[i] is not SetOperation set || set.Src is not CallExpr ce)
                        continue;

                    if (!_storage.TryGetByName(ce.Name, out var target) || target is null)
                        continue;

                    var sig = target.Signature;
                    bool changed = false;
                    var newArgs = new List<Expr>(ce.Args);

                    for (int a = 0; a < sig.Parameters.Count && a < newArgs.Count; a++)
                    {
                        if (!sig.Parameters[a].Type.IsCharPtr || newArgs[a] is StringExpr)
                            continue;

                        var mat = StringLiteralMaterializer.TryMaterialize(_parser.Image, newArgs[a], _imageLayout);
                        if (mat != null)
                        {
                            newArgs[a] = mat;
                            changed = true;
                        }
                    }

                    if (changed)
                        block.Operations[i] = new SetOperation(set.Dst, new CallExpr(ce.Name, newArgs));
                }
            }
        }
    }

    /// <summary>
    /// Конвертирует .dot файл в .svg с помощью Graphviz dot (для отладки CFG).
    /// </summary>
    private static void ConvertDotToSvg(string dotPath, string svgPath)
    {
        var proc = new Process();
        proc.StartInfo = new ProcessStartInfo("dot", $"-Tsvg \"{dotPath}\" -o \"{svgPath}\"")
        {
            UseShellExecute = false,
        };
        proc.Start();
        proc.WaitForExit();
    }
}

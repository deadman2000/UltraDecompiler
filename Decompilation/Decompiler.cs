using System.Diagnostics;
using System.Text;
using UltraDecompiler.CodeGeneration;
using UltraDecompiler.Compilation;
using UltraDecompiler.Disassembly.Parser;
using UltraDecompiler.Ir.Builder;
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

    /// <summary>
    /// Декомпилирует EXE/COM: находит <c>_main</c>, рекурсивно собирает функции,
    /// сопоставляет runtime с .LIB и сохраняет пользовательский код в <paramref name="outputDirectory"/>.
    /// </summary>
    /// <param name="libraryFileNames">
    /// Необязательный список имён или путей к конкретным .LIB для сопоставления.
    /// Если не задан, загружаются все *.LIB из <paramref name="libraryDirectory"/>.
    /// </param>
    public DecompileResult Decompile(
        string exePath,
        string libraryDirectory,
        string includeDirectory,
        string outputDirectory,
        bool exportGraph = false,
        IReadOnlyList<string>? libraryFileNames = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        // Загружаем образ программы
        var parser = new DosExeParser(exePath);
        var initRegisters = parser.IsCom ? RegisterState.InitCom : RegisterState.InitExe;
        var entryPoint = (int)parser.EntryPointOffset;

        var provider = new LibraryProvider(libraryDirectory, libraryFileNames);

        if (!provider.TryResolveMain(
                parser.Image,
                parser.RelocationTable,
                initRegisters,
                entryPoint,
                out var resolution))
        {
            return DecompileResult.Failed;
        }

        var mainOffset = resolution.MainOffset;

        // Собираем процедуры сначала с "безопасным" профилем (без TailReturnInserter),
        // чтобы получить сырые Instructions всех user-функций для точной детекции оптимизации.
        // Для /Od затем пересоберём с правильным профилем (один дополнительный проход — дёшево).
        var tempProfile = DecompilationProfileRegistry.GetProfile(OptimizationLevel.Enabled);
        var tempStorage = CollectProcedures(
            parser,
            provider,
            initRegisters,
            mainOffset,
            tempProfile);

        var optimizationLevel = OptimizationLevelHeuristics.DetectFromUserProcedures(tempStorage.All);

        var irProfile = DecompilationProfileRegistry.GetProfile(optimizationLevel);
        var storage = ReferenceEquals(irProfile, tempProfile)
            ? tempStorage
            : CollectProcedures(parser, provider, initRegisters, mainOffset, irProfile);

        if (exportGraph)
        {
            foreach (var proc in storage.All)
            {
                if (proc.IsLibrary || proc.Expressions == null)
                    continue;

                var exprDotPath = Path.Combine(outputDirectory, $"{proc.Name}.dot");
                var exprSvgPath = Path.Combine(outputDirectory, $"{proc.Name}.svg");
                proc.Expressions.SaveDot(exprDotPath);
                ConvertDotToSvg(exprDotPath, exprSvgPath);
            }
        }

        // Загружаем заголовки
        var headerCatalog = HeaderCatalog.Load(includeDirectory);

        // Подставляем функции (сигнатуры из заголовков .LIB или анализ тел пользовательских процедур).
        ProcedureSignatureResolver.ResolveAll(storage, headerCatalog);

        var imageLayout = ExeImageLayout.From(parser);

        // Подставляем в CallExpr (через CallState) имена (библиотечные) и аргументы по сигнатурам callee.
        // Для аргументов char* (из заголовков) near-указатель переводится в StringExpr по адресу в образе.
        CallSiteResolver.ResolveAll(storage, parser.Image, imageLayout);

        // Safety net после разрешения: гарантируем StringExpr для char* аргументов по типу из заголовка,
        // даже если CallExpr в IR не имел CallState на момент финального прохода.
        MaterializeCharPtrLiterals(storage, parser.Image, imageLayout);

        var compilerOptions = new CompilerOptions
        {
            MemoryModel = MemoryModelDetector.DetectFromLibraryFileName(resolution.PrimaryLibrary.FileName),
            StackCheckingEnabled = StackCheckDetector.Analyze(storage),
            OptimizationLevel = optimizationLevel,
        };

        var profile = DecompilationProfileRegistry.GetProfile(compilerOptions.OptimizationLevel);

        Directory.CreateDirectory(outputDirectory);
        var outputFiles = new List<string>();

        var userProcedures = storage.All
            .Where(static p => !p.IsLibrary)
            .OrderBy(static p => p.Offset)
            .ToList();

        // Собираем зависимости и готовим IR для кодогенерации.
        var preparedProcedures = new List<(DisassembledProcedure Procedure, IReadOnlyList<Operation> Operations)>();
        foreach (var procedure in userProcedures)
        {
            var postCtx = new PostProcessContext
            {
                Procedure = procedure,
                Storage = storage,
                HeaderCatalog = headerCatalog,
                Image = parser.Image,
                Layout = imageLayout,
                CompilerOptions = compilerOptions,
            };

            var operations = procedure.Expressions!.GetAllOperations();
            foreach (var pass in profile.GetProcedurePasses())
            {
                operations = pass.Apply(postCtx, operations);
                if (pass.Name == nameof(MainParameterNormalizer))
                {
                    procedure.Callees = ProcedureDependencyCollector.Collect(operations);
                }
            }

            preparedProcedures.Add((procedure, operations));
        }

        var globalRegistry = new GlobalVariableRegistry();
        for (var i = 0; i < preparedProcedures.Count; i++)
        {
            var (procedure, operations) = preparedProcedures[i];
            preparedProcedures[i] = (
                procedure,
                GlobalVariableMaterializer.Materialize(operations, globalRegistry, parser.Image, imageLayout));
        }

        // Заголовки — только при одном .c на процедуру (в объединённом .c прототипы не нужны).
        var referencedUserProcedures = ProcedureDependencyCollector.CollectReferencedUserProcedureNames(
            userProcedures,
            storage);

        if (preparedProcedures.Count == 1)
        {
            foreach (var procedure in userProcedures.Where(p => referencedUserProcedures.Contains(p.Name)))
            {
                var headerSource = CCodeGenerator.FormatHeaderFile(procedure.ToCodegenModel());
                var headerFileName = CCodeGenerator.FormatHeaderFileName(procedure.Name, procedure.Offset);
                var headerPath = Path.Combine(outputDirectory, headerFileName);
                File.WriteAllText(headerPath, headerSource, Encoding.ASCII);
                outputFiles.Add(headerPath);
            }
        }

        // Выбираем конфигурацию, соответствующую primary (или первую).
        var chosenConfig = resolution.PossibleLibraryConfigurations.FirstOrDefault(
            c => string.Equals(c.PrimaryCrtLibrary, resolution.PrimaryLibrary.FileName, StringComparison.OrdinalIgnoreCase))
            ?? resolution.PossibleLibraryConfigurations[0];

        // Раскладка: если >1 процедуры — один combined .c; иначе — отдельный .c по имени процедуры (для round-trip).
        var combinedUnits = new List<(ProcedureCodegenModel Procedure, IReadOnlyList<Operation> Operations, IReadOnlyList<string> Includes)>();
        foreach (var (procedure, operations) in preparedProcedures)
        {
            var includes = ProcedureIncludeResolver.ResolveIncludes(
                procedure,
                procedure.Callees,
                storage,
                headerCatalog);

            combinedUnits.Add((procedure.ToCodegenModel(), operations, includes));
        }

        List<string> makefileSourceFileNames;
        if (preparedProcedures.Count > 1)
        {
            var combinedFileName = CCodeGenerator.FormatCombinedSourceFileName(Path.GetFileName(exePath));
            var combinedSource = CCodeGenerator.FormatCombinedCSource(combinedUnits, globalRegistry.All);
            var combinedPath = Path.Combine(outputDirectory, combinedFileName);
            File.WriteAllText(combinedPath, combinedSource, Encoding.ASCII);
            outputFiles.Add(combinedPath);
            makefileSourceFileNames = [combinedFileName];
        }
        else
        {
            var (procedure, operations) = preparedProcedures[0];
            var includes = combinedUnits[0].Includes;
            var source = CCodeGenerator.FormatCSource(procedure.ToCodegenModel(), operations, includes, globalRegistry.All);
            var fileName = CCodeGenerator.FormatOutputFileName(procedure.Name, procedure.Offset);
            var filePath = Path.Combine(outputDirectory, fileName);
            File.WriteAllText(filePath, source, Encoding.ASCII);
            outputFiles.Add(filePath);
            makefileSourceFileNames = [fileName];
        }

        var makefilePath = MakefileGenerator.WriteMakefile(
            new MakefileOptions
            {
                TargetExeFileName = Path.GetFileName(exePath),
                SourceFileNames = makefileSourceFileNames,
                CompilerOptions = compilerOptions,
                LibraryFileNames = chosenConfig.LibraryFileNames,
                OutputDirectory = Path.GetFullPath(outputDirectory),
            },
            outputDirectory);
        outputFiles.Add(makefilePath);

        return new DecompileResult
        {
            Success = true,
            MainOffset = mainOffset,
            LinkedLibraryFileNames = chosenConfig.LibraryFileNames,
            PossibleLibraryConfigurations = resolution.PossibleLibraryConfigurations,
            Procedures = storage,
            OutputFiles = outputFiles,
            CompilerOptions = compilerOptions,
        };
    }

    private ProcedureStorage CollectProcedures(
        DosExeParser parser,
        LibraryProvider provider,
        RegisterState initRegisters,
        int initOffset,
        IDecompilationProfile profile)
    {
        var storage = new ProcedureStorage();
        var pending = new Queue<int>();
        pending.Enqueue(initOffset);

        // Очередь для рекурсивного разбора функций, начиная с entry (в т.ч. library match),
        // добавляем "заглушки" (stub) в storage. Expressions будут заполнены позже, когда мы их разберём.
        while (pending.Count > 0)
        {
            var offset = pending.Dequeue();

            if (storage.Contains(offset))
                continue;

            var instructions = X86Disassembler.Disassemble(
                parser.Image,
                parser.RelocationTable,
                offset,
                initRegisters);

            if (instructions.Count == 0)
                continue;

            // Проверяем library match до разбора — экономит время provider
            var libraryMatch = provider.TryMatchProcedure(
                parser.Image,
                parser.RelocationTable,
                offset,
                initRegisters);

            DisassembledProcedure proc;
            if (libraryMatch is not null)
            {
                proc = new DisassembledProcedure
                {
                    Offset = offset,
                    Instructions = instructions,
                    Name = LinkerSymbolNames.ToCName(libraryMatch.SymbolName),
                    IsLibrary = true,
                    LibraryMatch = libraryMatch,
                };
                storage.Add(proc);
                // Для library не разбираем и не ставим в очередь (не enqueue)
                continue;
            }

            var cfg = new ControlFlowGraph();
            cfg.BuildFromInstructions(instructions, offset, initRegisters);

            var expressions = new ExpressionBuilder();
            expressions.BuildProc(cfg);

            var name = offset == initOffset
                ? MainFunction
                : $"sub_{offset:X4}";
            proc = new DisassembledProcedure
            {
                Offset = offset,
                Instructions = instructions,
                Expressions = expressions,
                Name = name,
                IsLibrary = false,
            };

            profile.ApplyIrConstructionPasses(new IrConstructionContext
            {
                Builder = expressions,
                Graph = cfg,
                Procedure = proc,
            });

            storage.Add(proc);

            EnqueueExternalTargets(instructions, pending);
        }

        return storage;
    }

    /// <summary>
    /// Ставим в очередь внешние цели вызовов и переходов для рекурсивного разбора
    /// </summary>
    private static void EnqueueExternalTargets(IReadOnlyList<Instruction> instructions, Queue<int> pending)
    {
        var functionOffsets = instructions.Select(static i => i.Offset).Distinct();

        foreach (var instr in instructions)
        {
            if (!instr.IsCall && !instr.IsUnconditionalJump)
            {
                continue;
            }

            var target = instr.JumpTarget;
            if (target >= 0 && !functionOffsets.Contains(target))
            {
                pending.Enqueue(target);
            }
        }
    }

    /// <summary>
    /// Ищем по всем процедурам и для всех CallExpr (по эвристике) near-аргументы char*,
    /// превращаем ConstExpr/ImageOffsetExpr в StringExpr если они указывают на строковый литерал.
    /// Это позволяет материализовать "printf("...")" на этапе, когда мы знаем сигнатуры.
    /// </summary>
    private static void MaterializeCharPtrLiterals(ProcedureStorage storage, byte[] image, ExeImageLayout layout)
    {
        if (image.Length == 0) return;

        foreach (var proc in storage.All)
        {
            if (proc.Expressions == null)
                continue;

            foreach (var block in proc.Expressions.Blocks)
            {
                for (int i = 0; i < block.Operations.Count; i++)
                {
                    if (block.Operations[i] is SetOperation set && set.Src is CallExpr ce)
                    {
                        if (!storage.TryGetByName(ce.Name, out var target) || target is null) continue;
                        var sig = target.Signature;
                        bool changed = false;
                        var newArgs = new List<Expr>(ce.Args);
                        for (int a = 0; a < sig.Parameters.Count && a < newArgs.Count; a++)
                        {
                            if (!sig.Parameters[a].Type.IsCharPtr || newArgs[a] is StringExpr)
                                continue;

                            var mat = StringLiteralMaterializer.TryMaterialize(image, newArgs[a], layout);
                            if (mat != null)
                            {
                                newArgs[a] = mat;
                                changed = true;
                            }
                        }
                        if (changed)
                        {
                            block.Operations[i] = new SetOperation(set.Dst, new CallExpr(ce.Name, newArgs));
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Вспомогательный метод для конвертации .dot в .svg (для отладки CFG).
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

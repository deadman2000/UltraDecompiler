using System.Text;
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
    private const string MainFunction = "main";

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

        // Парсим исполняемый файл
        var parser = new DosExeParser(exePath);
        var initRegisters = parser.IsCom ? RegisterState.InitCom : RegisterState.InitExe;
        var entryPoint = (int)parser.EntryPointOffset;

        var provider = new LibraryProvider(libraryDirectory);

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

        // Дизассемблируем main и все вложенные переходы.
        var storage = CollectProcedures(
            parser,
            provider,
            initRegisters,
            mainOffset);

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
            StackCheckingEnabled = StackCheckDetector.Analyze(storage, parser.Image),
        };

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
            var operations = procedure.Expressions.GetAllOperations();
            operations = StackCheckDetector.RemoveChkstkCalls(operations);
            operations = OperationOptimizer.Optimize(operations);
            procedure.Callees = ProcedureDependencyCollector.Collect(operations);
            preparedProcedures.Add((procedure, operations));
        }

        // Заголовки только для пользовательских процедур, вызываемых из других (main и точка входа — без .h).
        var referencedUserProcedures = ProcedureDependencyCollector.CollectReferencedUserProcedureNames(
            userProcedures,
            storage);

        foreach (var procedure in userProcedures.Where(p => referencedUserProcedures.Contains(p.Name)))
        {
            var headerSource = CCodeGenerator.FormatHeaderFile(procedure);
            var headerFileName = CCodeGenerator.FormatHeaderFileName(procedure.Name, procedure.Offset);
            var headerPath = Path.Combine(outputDirectory, headerFileName);
            File.WriteAllText(headerPath, headerSource, Encoding.ASCII);
            outputFiles.Add(headerPath);
        }

        // Исходники с #include на зависимости.
        foreach (var (procedure, operations) in preparedProcedures)
        {
            var includes = ProcedureIncludeResolver.ResolveIncludes(
                procedure,
                procedure.Callees,
                storage,
                headerCatalog);

            var source = CCodeGenerator.FormatCSource(procedure, operations, includes);
            var fileName = CCodeGenerator.FormatOutputFileName(procedure.Name, procedure.Offset);
            var filePath = Path.Combine(outputDirectory, fileName);
            File.WriteAllText(filePath, source, Encoding.ASCII);
            outputFiles.Add(filePath);
        }

        // Выбираем конфигурацию, соответствующую primary (или первую)
        var chosenConfig = resolution.PossibleLibraryConfigurations.FirstOrDefault(
            c => string.Equals(c.PrimaryCrtLibrary, resolution.PrimaryLibrary.FileName, StringComparison.OrdinalIgnoreCase))
            ?? resolution.PossibleLibraryConfigurations[0];

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
        int initOffset)
    {
        var storage = new ProcedureStorage();
        var pending = new Queue<int>();
        pending.Enqueue(initOffset);

        // Обнаруживаем все достижимые процедуры, определяем имена (в т.ч. library match),
        // добавляем "скелеты" (stub) в storage. Expressions будут построены позже, когда все имена известны.
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

            // Матчинг и narrowing кандидатов полностью внутри provider
            var libraryMatch = provider.TryMatchProcedure(
                parser.Image,
                parser.RelocationTable,
                offset,
                initRegisters);

            var cfg = new ControlFlowGraph();
            cfg.BuildFromInstructions(instructions, offset, parser.Image, initRegisters);

            var expressions = new ExpressionBuilder();
            expressions.BuildProc(cfg, storage);

            DisassembledProcedure proc;
            if (libraryMatch is not null)
            {
                proc = new DisassembledProcedure
                {
                    Offset = offset,
                    Instructions = instructions,
                    Expressions = expressions,
                    Name = LinkerSymbolNames.ToCName(libraryMatch.SymbolName),
                    IsLibrary = true,
                    LibraryMatch = libraryMatch,
                };
                storage.Add(proc);
                // Для library не сканируем её внутренние вызовы (не enqueue)
                continue;
            }

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
            storage.Add(proc);

            EnqueueExternalTargets(parser.Image, instructions, pending);
        }

        return storage;
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

    /// <summary>
    /// Проходит по всем операциям и для вызовов, чья сигнатура (из заголовка) имеет параметр char*,
    /// преобразует соответствующий ConstExpr/ImageOffsetExpr в StringExpr путём чтения литерала.
    /// Это обеспечивает восстановление "printf("...")" по типу, без эвристик в кодогенераторе.
    /// </summary>
    private static void MaterializeCharPtrLiterals(ProcedureStorage storage, byte[] image, ExeImageLayout layout)
    {
        if (image.Length == 0) return;

        foreach (var proc in storage.All)
        {
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

}

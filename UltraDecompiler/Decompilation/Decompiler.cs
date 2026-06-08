using System.Text;
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

        // Единая точка работы с библиотеками: передаём только путь к папке.
        // Вся логика сопоставления crt0, поиска _main, narrowing и матчинга — внутри LibraryProvider.
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
        // Provider занимается матчингом библиотечных процедур и сужением кандидатов.
        var storage = CollectProcedures(
            parser,
            provider,
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

            if (libraryMatch is not null)
            {
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

}

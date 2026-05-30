using Common;
using LibParser.Models;
using LibParser.Omf;
using UltraDecompiler.Disassembler;

namespace LibMatching;

/// <summary>
/// Сопоставляет участок дизассемблированного EXE/COM с публичными символами OMF-библиотеки QuickC.
/// </summary>
/// <remarks>
/// Алгоритм: извлечь линейное тело функции из образа программы, затем перебрать символы
/// словаря .LIB и сравнить с кодом соответствующего модуля. Словарь OMF хранит только
/// «символ → страница модуля», без смещения внутри CODE; пока предполагаем, что публичная
/// функция начинается с offset 0 сегмента _TEXT (типично для однофайловых модулей QuickC).
/// </remarks>
public static class LibraryFunctionMatcher
{
    /// <summary>
    /// Ищет в <paramref name="library"/> символы, чьё тело функции совпадает с кодом по смещению
    /// <paramref name="imageOffset"/> в образе программы (инициализация регистров — <see cref="RegisterState.InitExe"/>).
    /// </summary>
    public static IReadOnlyList<LibraryMatchResult> Match(
        byte[] image,
        RelocationTable imageRelocations,
        int imageOffset,
        OmfLibrary library) =>
        Match(image, imageRelocations, imageOffset, library, RegisterState.InitExe);

    /// <summary>
    /// Ищет в <paramref name="library"/> символы, чьё тело функции совпадает с кодом по смещению
    /// <paramref name="imageOffset"/> в образе программы.
    /// </summary>
    public static IReadOnlyList<LibraryMatchResult> Match(
        byte[] image,
        RelocationTable imageRelocations,
        int imageOffset,
        OmfLibrary library,
        RegisterState initRegisters)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(imageRelocations);
        ArgumentNullException.ThrowIfNull(library);

        var targetBody = FunctionBodyExtractor.Extract(image, imageRelocations, imageOffset, initRegisters);
        if (targetBody.Count == 0)
        {
            return [];
        }

        var results = new List<LibraryMatchResult>();

        // Несколько символов словаря могут указывать на один модуль (алиасы, C- и asm-имена).
        // Сравнение тела функции для страницы выполняем один раз.
        var checkedModules = new Dictionary<(ushort Page, int CodeOffset), bool>();

        foreach (var (symbolName, symbol) in library.Symbols)
        {
            var module = library.GetModuleByPage(symbol.ModulePage);
            if (module is null)
            {
                continue;
            }

            var codeSegment = module.CodeSegments.FirstOrDefault();
            if (codeSegment is null || codeSegment.Data.Length == 0)
            {
                continue;
            }

            // TODO: когда появится разбор PUBDEF — искать точку входа символа внутри сегмента.
            const int moduleCodeOffset = 0;
            var cacheKey = (symbol.ModulePage, moduleCodeOffset);
            if (!checkedModules.TryGetValue(cacheKey, out var matches))
            {
                matches = TryMatchModule(targetBody, module, codeSegment, moduleCodeOffset);
                checkedModules[cacheKey] = matches;
            }

            if (matches)
            {
                results.Add(new LibraryMatchResult
                {
                    SymbolName = symbolName,
                    ModulePage = symbol.ModulePage,
                    ModuleName = module.DisplayName,
                    ModuleCodeOffset = moduleCodeOffset,
                });
            }
        }

        return results;
    }

    private static bool TryMatchModule(
        IReadOnlyList<Instruction> targetBody,
        OmfModule module,
        OmfSegmentData codeSegment,
        int moduleCodeOffset)
    {
        if (moduleCodeOffset < 0 || moduleCodeOffset >= codeSegment.Data.Length)
        {
            return false;
        }

        try
        {
            // FIXUPP модуля описывают, какие 16-битные слова в .LIB ещё «символические».
            var libraryRelocations = OmfRelocationTableBuilder.Build(codeSegment, module.Fixups);
            var libraryBody = FunctionBodyExtractor.Extract(
                codeSegment.Data,
                libraryRelocations,
                moduleCodeOffset,
                RegisterState.Unknown);

            return FunctionBodyComparer.AreEquivalent(targetBody, libraryBody, libraryRelocations);
        }
        catch (IndexOutOfRangeException)
        {
            // Некоторые модули .LIB (напр. CLIBFP) содержат CODE, который дизасsemblер не может
            // полностью разобрать — такой модуль просто не совпадает.
            return false;
        }
    }
}

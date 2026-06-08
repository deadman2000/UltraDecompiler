using Common;
using LibParser.Models;
using LibParser.Omf;

namespace UltraDecompiler.LibMatching;

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
        RegisterState initRegisters) =>
        Match(image, imageRelocations, imageOffset, library, initRegisters, symbolName: null, moduleName: null);

    /// <summary>
    /// Ищет в <paramref name="library"/> символы, чьё тело функции совпадает с кодом по смещению
    /// <paramref name="imageOffset"/> в образе программы.
    /// </summary>
    /// <param name="symbolName">
    /// Если задано — проверяется только этот символ словаря (например <c>__astart</c>).
    /// </param>
    /// <param name="moduleName">
    /// Если задано — проверяются только символы указанного модуля (например <c>crt0</c>).
    /// </param>
    public static IReadOnlyList<LibraryMatchResult> Match(
        byte[] image,
        RelocationTable imageRelocations,
        int imageOffset,
        OmfLibrary library,
        RegisterState initRegisters,
        string? symbolName,
        string? moduleName)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(imageRelocations);
        ArgumentNullException.ThrowIfNull(library);

        // Безусловные JMP ниже imageOffset не обходятся (напр. __chkstk → crt0).
        var targetBody = X86Disassembler.Disassemble(
            image,
            imageRelocations,
            imageOffset,
            initRegisters,
            minJumpTarget: imageOffset);
        if (targetBody.Count == 0)
        {
            return [];
        }

        var results = new List<LibraryMatchResult>();

        // Несколько символов словаря могут указывать на один модуль (алиасы, C- и asm-имена).
        // Сравнение тела функции для страницы выполняем один раз.
        var checkedModules = new Dictionary<(ushort Page, int CodeOffset), bool>();

        foreach (var (currentSymbolName, symbol) in library.Symbols)
        {
            if (symbolName is not null
                && !string.Equals(currentSymbolName, symbolName, StringComparison.Ordinal))
            {
                continue;
            }

            var module = library.GetModuleByPage(symbol.ModulePage);
            if (module is null)
            {
                continue;
            }

            if (moduleName is not null
                && !module.DisplayName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
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
                    SymbolName = currentSymbolName,
                    ModulePage = symbol.ModulePage,
                    ModuleName = module.DisplayName,
                    ModuleCodeOffset = moduleCodeOffset,
                });
            }
        }

        return results;
    }

    public static bool TryMatchModule(
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
            var libraryBody = X86Disassembler.Disassemble(
                codeSegment.Data,
                libraryRelocations,
                moduleCodeOffset,
                RegisterState.Unknown,
                minJumpTarget: moduleCodeOffset);

            return FunctionBodyComparer.AreEquivalent(targetBody, libraryBody, libraryRelocations);
        }
        catch (IndexOutOfRangeException)
        {
            // Некоторые модули .LIB (напр. CLIBFP) содержат CODE, который дизассемблер не может
            // полностью разобрать — такой модуль просто не совпадает.
            // TODO добавить поддержку остальных опкодов
            return false;
        }
    }
}

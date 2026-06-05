using Common;
using LibParser.Models;

namespace UltraDecompiler.LibMatching;

public sealed class LibMatcher
{
    /// <summary>
    /// Сопоставляет точку входа с символами OMF-библиотек.
    /// </summary>
    /// <param name="symbolName">Если задано — проверяется только этот символ.</param>
    /// <param name="moduleName">Если задано — проверяются только символы указанного модуля.</param>
    public IReadOnlyList<EntryPointLibraryMatchInfo> MatchEntryPoint(
        byte[] image,
        RelocationTable imageRelocations,
        int entryPointOffset,
        IReadOnlyList<OmfLibrary> libraries,
        RegisterState initRegisters,
        string? symbolName = null,
        string? moduleName = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(imageRelocations);
        ArgumentNullException.ThrowIfNull(libraries);

        var results = new List<EntryPointLibraryMatchInfo>();

        foreach (var library in libraries)
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

    public IReadOnlyList<LibraryMatchInfo> MatchFunction(
        byte[] image,
        RelocationTable imageRelocations,
        int imageOffset,
        OmfLibrary library,
        RegisterState initRegisters) =>
        MatchFunction(image, imageRelocations, imageOffset, library, initRegisters, symbolName: null, moduleName: null);

    /// <summary>
    /// Сопоставляет участок образа с символами одной OMF-библиотеки.
    /// </summary>
    public IReadOnlyList<LibraryMatchInfo> MatchFunction(
        byte[] image,
        RelocationTable imageRelocations,
        int imageOffset,
        OmfLibrary library,
        RegisterState initRegisters,
        string? symbolName,
        string? moduleName) =>
        LibraryFunctionMatcher.Match(
                image,
                imageRelocations,
                imageOffset,
                library,
                initRegisters,
                symbolName,
                moduleName)
            .Select(m => ToMatchInfo(m, library))
            .ToList();

    public int FindMainOffset(
        byte[] image,
        RelocationTable imageRelocations,
        OmfLibrary library,
        int astartOffset,
        RegisterState initRegisters,
        int astartModuleCodeOffset) =>
        MainOffsetFinder.FindFromAstart(
            image,
            imageRelocations,
            library,
            astartOffset,
            initRegisters,
            astartModuleCodeOffset);

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

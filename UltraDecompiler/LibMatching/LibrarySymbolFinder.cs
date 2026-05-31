using Common;
using LibParser.Models;

namespace UltraDecompiler.LibMatching;

/// <summary>Поиск смещения публичного символа в образе EXE/COM по сопоставлению с OMF-библиотекой.</summary>
public static class LibrarySymbolFinder
{
    /// <summary>
    /// Перебирает смещения образа и возвращает первое, где <paramref name="symbolName"/>
    /// найден через <see cref="LibraryFunctionMatcher"/>.
    /// </summary>
    public static int Find(
        byte[] image,
        RelocationTable imageRelocations,
        OmfLibrary library,
        string symbolName) =>
        Find(image, imageRelocations, library, symbolName, RegisterState.InitExe);

    /// <summary>
    /// Перебирает смещения образа и возвращает первое, где найден <paramref name="symbolName"/>.
    /// </summary>
    public static int Find(
        byte[] image,
        RelocationTable imageRelocations,
        OmfLibrary library,
        string symbolName,
        RegisterState initRegisters)
    {
        if (!TryFind(image, imageRelocations, library, symbolName, initRegisters, out var offset))
        {
            throw new InvalidOperationException(
                $"Символ {symbolName} не найден в образе (размер {image.Length} байт).");
        }

        return offset;
    }

    /// <summary>Пытается найти смещение символа в образе.</summary>
    public static bool TryFind(
        byte[] image,
        RelocationTable imageRelocations,
        OmfLibrary library,
        string symbolName,
        RegisterState initRegisters,
        out int offset)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(imageRelocations);
        ArgumentNullException.ThrowIfNull(library);

        for (offset = 0; offset < image.Length; offset++)
        {
            var matches = LibraryFunctionMatcher.Match(
                image,
                imageRelocations,
                offset,
                library,
                initRegisters);

            if (matches.Any(m => m.SymbolName == symbolName))
            {
                return true;
            }
        }

        offset = 0;
        return false;
    }
}

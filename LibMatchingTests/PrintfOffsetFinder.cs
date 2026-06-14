using LibParser.Models;
using UltraDecompiler.LibMatching;

namespace LibMatchingTests;

/// <summary>Ищет смещение <c>_printf</c> в образе EXE перебором точек входа.</summary>
internal static class PrintfOffsetFinder
{
    public static int Find(DosExeParser parser, OmfLibrary library)
    {
        for (var offset = 0; offset < parser.Image.Length; offset++)
        {
            if (parser.Image[offset] != 0x55)
            {
                continue;
            }

            var matches = LibraryFunctionMatcher.Match(
                parser.Image,
                parser.RelocationTable,
                offset,
                library,
                RegisterState.InitExe);

            if (matches.Any(static m => m.SymbolName == "_printf"))
            {
                return offset;
            }
        }

        throw new InvalidOperationException(
            $"Символ _printf не найден в образе (размер {parser.Image.Length} байт).");
    }
}

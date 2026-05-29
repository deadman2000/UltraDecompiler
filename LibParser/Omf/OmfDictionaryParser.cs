namespace LibParser.Omf;

using LibParser.Models;

/// <summary>Разбор хеш-словаря OMF-библиотеки (512-байтные блоки).</summary>
internal static class OmfDictionaryParser
{
    private const int BlockSize = 512;
    private const int BucketCount = 37;
    private const int FreeSpaceIndex = 37;
    private const int DataStart = 38;

    public static Dictionary<string, OmfPublicSymbol> Parse(
        ReadOnlySpan<byte> data,
        int dictionaryOffset,
        ushort blockCount,
        bool caseSensitive)
    {
        var comparer = caseSensitive
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;

        var symbols = new Dictionary<string, OmfPublicSymbol>(comparer);
        var totalSize = blockCount * BlockSize;
        if (dictionaryOffset + totalSize > data.Length)
        {
            throw new InvalidDataException("Словарь выходит за пределы файла.");
        }

        for (var blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            var blockOffset = dictionaryOffset + blockIndex * BlockSize;
            var block = data.Slice(blockOffset, BlockSize);
            var endOffset = block[FreeSpaceIndex] == 255
                ? BlockSize
                : block[FreeSpaceIndex] * 2;
            if (endOffset < DataStart)
            {
                endOffset = DataStart;
            }

            if (endOffset > BlockSize)
            {
                endOffset = BlockSize;
            }

            ParseBucketEntries(block, endOffset, symbols);
            ParseEntries(block, endOffset, symbols);
        }

        return symbols;
    }

    private static void ParseBucketEntries(
        ReadOnlySpan<byte> block,
        int endOffset,
        Dictionary<string, OmfPublicSymbol> symbols)
    {
        for (var bucket = 0; bucket < BucketCount; bucket++)
        {
            var offset = block[bucket] * 2;
            if (offset >= DataStart && offset < endOffset)
            {
                TryAddEntry(block, offset, endOffset, symbols);
            }
        }
    }

    private static void ParseEntries(
        ReadOnlySpan<byte> block,
        int endOffset,
        Dictionary<string, OmfPublicSymbol> symbols)
    {
        for (var offset = DataStart; offset < endOffset; offset++)
        {
            TryAddEntry(block, offset, endOffset, symbols);
        }
    }

    private static void TryAddEntry(
        ReadOnlySpan<byte> block,
        int offset,
        int endOffset,
        Dictionary<string, OmfPublicSymbol> symbols)
    {
        var nameLength = block[offset];
        if (nameLength < 1 || nameLength > 255 || offset + 1 + nameLength + 2 > endOffset)
        {
            return;
        }

        var nameBytes = block.Slice(offset + 1, nameLength);
        if (!IsAsciiIdentifier(nameBytes))
        {
            return;
        }

        var name = System.Text.Encoding.ASCII.GetString(nameBytes);
        var page = OmfBinaryReader.ReadUInt16At(block, offset + 1 + nameLength);
        symbols[name] = new OmfPublicSymbol { Name = name, ModulePage = page };
    }

    private static bool IsAsciiIdentifier(ReadOnlySpan<byte> name)
    {
        if (name.IsEmpty)
        {
            return false;
        }

        foreach (var b in name)
        {
            var c = (char)b;
            if (char.IsLetterOrDigit(c) || c is '_' or '@' or '?' or '$')
            {
                continue;
            }

            return false;
        }

        return true;
    }
}

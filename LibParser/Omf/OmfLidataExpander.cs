namespace LibParser.Omf;

/// <summary>Разворачивает LIDATA (A2h/A3h) в плоский массив байт.</summary>
internal static class OmfLidataExpander
{
    /// <summary>Разворачивает поле Data Block записи LIDATA (без индекса сегмента и смещения).</summary>
    public static byte[] ExpandData(ReadOnlySpan<byte> dataBlocks, bool use32BitOffsets)
    {
        var reader = new OmfBinaryReader(dataBlocks);
        using var output = new MemoryStream();
        ExpandDataBlock(reader, output, use32BitOffsets);
        return output.ToArray();
    }

    private static void ExpandDataBlock(OmfBinaryReader reader, MemoryStream output, bool use32BitOffsets)
    {
        var repeatCount = use32BitOffsets ? (int)reader.ReadUInt32() : reader.ReadUInt16();
        var blockCount = reader.ReadUInt16();

        if (blockCount == 0)
        {
            var byteCount = reader.ReadByte();
            var chunk = reader.ReadBytes(byteCount);
            for (var i = 0; i < repeatCount; i++)
            {
                output.Write(chunk);
            }

            return;
        }

        using var inner = new MemoryStream();
        for (var i = 0; i < blockCount; i++)
        {
            ExpandDataBlock(reader, inner, use32BitOffsets);
        }

        var innerBytes = inner.ToArray();
        for (var i = 0; i < repeatCount; i++)
        {
            output.Write(innerBytes);
        }
    }
}

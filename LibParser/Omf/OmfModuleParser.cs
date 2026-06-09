namespace LibParser.Omf;

using LibParser.Models;

/// <summary>Разбор одного объектного модуля внутри библиотеки.</summary>
internal static class OmfModuleParser
{
    private sealed class SegmentInfo
    {
        public required string SegmentName { get; init; }

        public required string ClassName { get; init; }

        public List<(int Offset, byte[] Bytes)> Chunks { get; } = [];
    }

    public static OmfModule Parse(
        ReadOnlySpan<byte> moduleData,
        ushort pageNumber,
        int fileOffset)
    {
        var names = new List<string> { string.Empty };
        var segmentNames = new List<string>();
        var groupNames = new List<string>();
        var externals = new Dictionary<int, string>();
        var externalList = new List<OmfExternalSymbol>();
        var segments = new Dictionary<int, SegmentInfo>();
        var fixups = new List<OmfFixup>();
        var publicSymbols = new List<OmfModulePublicSymbol>();
        var fixupThreads = new OmfFixuppThreadState();

        string headerName = string.Empty;
        string? libModName = null;
        var lastDataSegmentIndex = 0;
        var lastDataSegmentOffset = 0;

        var pos = 0;
        while (pos < moduleData.Length)
        {
            var recordType = moduleData[pos];
            var recordLength = OmfBinaryReader.ReadUInt16At(moduleData, pos + 1);
            var contentStart = pos + 3;
            var recordEnd = pos + 3 + recordLength;
            if (recordEnd > moduleData.Length)
            {
                break;
            }

            var contentLength = recordLength > 0 ? recordLength - 1 : 0;
            var content = moduleData.Slice(contentStart, contentLength);

            switch (recordType)
            {
                case OmfRecordTypes.Theadr or OmfRecordTypes.Lheadr:
                    headerName = ReadHeaderName(content);
                    break;

                case OmfRecordTypes.Coment:
                    libModName ??= TryReadLibModName(content);
                    break;

                case OmfRecordTypes.Extdef:
                    ParseExtdef(content, externals, externalList);
                    break;

                case OmfRecordTypes.Lnames:
                    ParseLnames(content, names);
                    break;

                case OmfRecordTypes.Segdef:
                case OmfRecordTypes.Segdef32:
                    ParseSegdef(content, recordType, names, segments, segmentNames);
                    break;

                case OmfRecordTypes.Grpdef:
                    ParseGrpdef(content, names, groupNames);
                    break;

                case OmfRecordTypes.Pubdef:
                case OmfRecordTypes.Pubdef32:
                    ParsePubdef(content, recordType, publicSymbols);
                    break;

                case OmfRecordTypes.Ledata:
                case OmfRecordTypes.Ledata32:
                    (lastDataSegmentIndex, lastDataSegmentOffset) =
                        AppendLedata(content, recordType, segments);
                    break;

                case OmfRecordTypes.Lidata:
                case OmfRecordTypes.Lidata32:
                    (lastDataSegmentIndex, lastDataSegmentOffset) =
                        AppendLidata(content, recordType, segments);
                    break;

                case OmfRecordTypes.Fixup:
                case OmfRecordTypes.Fixup32:
                    {
                        var result = OmfFixuppParser.Parse(
                            content,
                            recordType,
                            fixupThreads,
                            lastDataSegmentIndex,
                            lastDataSegmentOffset);
                        fixupThreads = result.Threads;
                        if (lastDataSegmentIndex != 0)
                        {
                            foreach (var fixup in result.Fixups)
                            {
                                fixups.Add(OmfFixupNameResolver.WithResolvedNames(
                                    fixup,
                                    segmentNames,
                                    groupNames,
                                    externals));
                            }
                        }
                    }

                    break;

                case OmfRecordTypes.Modend:
                case OmfRecordTypes.Modend32:
                    pos = moduleData.Length;
                    continue;
            }

            pos = recordEnd;
        }

        var segmentList = BuildSegmentData(segments);
        return new OmfModule
        {
            HeaderName = headerName,
            LibraryModuleName = libModName,
            PageNumber = pageNumber,
            FileOffset = fileOffset,
            RawData = moduleData.ToArray(),
            Segments = segmentList,
            ExternalSymbols = externalList,
            Fixups = fixups,
            PublicSymbols = publicSymbols,
        };
    }

    private static string ReadHeaderName(ReadOnlySpan<byte> content)
    {
        if (content.IsEmpty)
        {
            return string.Empty;
        }

        var nameLength = content[0];
        if (nameLength == 0 || 1 + nameLength > content.Length)
        {
            return string.Empty;
        }

        return System.Text.Encoding.ASCII.GetString(content.Slice(1, nameLength));
    }

    private static string? TryReadLibModName(ReadOnlySpan<byte> content)
    {
        var classOffset = content.Length > 1 && content[1] == OmfRecordTypes.ComentClassLibMod ? 2 : -1;
        if (classOffset < 0 && content.Length > 2 && content[2] == OmfRecordTypes.ComentClassLibMod)
        {
            classOffset = 3;
        }

        if (classOffset < 0 || classOffset >= content.Length)
        {
            return null;
        }

        var reader = new OmfBinaryReader(content, classOffset);
        return reader.ReadCountedAscii();
    }

    private static void ParseExtdef(
        ReadOnlySpan<byte> content,
        Dictionary<int, string> externals,
        List<OmfExternalSymbol> externalList)
    {
        var reader = new OmfBinaryReader(content);
        while (!reader.End)
        {
            var name = reader.ReadCountedAscii();
            if (string.IsNullOrEmpty(name))
            {
                break;
            }

            if (!reader.End)
            {
                _ = reader.ReadIndex(); // type index
            }

            var index = externalList.Count + 1;
            externals[index] = name;
            externalList.Add(new OmfExternalSymbol { Index = index, Name = name });
        }
    }

    private static void ParseLnames(ReadOnlySpan<byte> content, List<string> names)
    {
        var reader = new OmfBinaryReader(content);
        while (!reader.End)
        {
            names.Add(reader.ReadCountedAscii());
        }
    }

    private static void ParsePubdef(
        ReadOnlySpan<byte> content,
        byte recordType,
        List<OmfModulePublicSymbol> publicSymbols)
    {
        var reader = new OmfBinaryReader(content);
        if (!reader.TryReadIndex(out var groupIndex))
        {
            return;
        }

        if (!reader.TryReadIndex(out var segmentIndex))
        {
            return;
        }

        // Абсолютная адресация: при нулевых индексах группы и сегмента следует Base Frame (16 бит).
        if (groupIndex == 0 && segmentIndex == 0 && !reader.TryReadUInt16(out _))
        {
            return;
        }

        var use32BitOffset = recordType == OmfRecordTypes.Pubdef32;
        while (!reader.End)
        {
            var name = reader.ReadCountedAscii();
            if (string.IsNullOrEmpty(name))
            {
                break;
            }

            var offset = use32BitOffset
                ? (int)reader.ReadUInt32()
                : reader.ReadUInt16();
            _ = reader.ReadIndex();

            publicSymbols.Add(new OmfModulePublicSymbol
            {
                Name = name,
                SegmentIndex = segmentIndex,
                Offset = offset,
            });
        }
    }

    private static void ParseGrpdef(
        ReadOnlySpan<byte> content,
        List<string> names,
        List<string> groupNames)
    {
        var reader = new OmfBinaryReader(content);
        if (!reader.TryReadIndex(out var groupNameIndex))
        {
            return;
        }

        groupNames.Add(GetName(names, groupNameIndex));

        while (!reader.End)
        {
            var descriptor = reader.ReadByte();
            if (descriptor != 0xFF)
            {
                break;
            }

            _ = reader.ReadIndex();
        }
    }

    private static void ParseSegdef(
        ReadOnlySpan<byte> content,
        byte recordType,
        List<string> names,
        Dictionary<int, SegmentInfo> segments,
        List<string> segmentNames)
    {
        var reader = new OmfBinaryReader(content);
        var acbp = reader.ReadByte();
        var alignment = (acbp >> 5) & 0x07;
        if (alignment == 0)
        {
            reader.Skip(3);
        }

        _ = recordType == OmfRecordTypes.Segdef32 ? reader.ReadUInt32() : reader.ReadUInt16();

        var segNameIndex = reader.ReadIndex();
        var classNameIndex = reader.ReadIndex();
        _ = reader.ReadIndex();

        var segmentName = GetName(names, segNameIndex);
        var className = GetName(names, classNameIndex);
        var segmentIndex = segments.Count + 1;
        segments[segmentIndex] = new SegmentInfo
        {
            SegmentName = segmentName,
            ClassName = className,
        };
        segmentNames.Add(segmentName);
    }

    private static (int SegmentIndex, int SegmentOffset) AppendLedata(
        ReadOnlySpan<byte> content,
        byte recordType,
        Dictionary<int, SegmentInfo> segments)
    {
        var reader = new OmfBinaryReader(content);
        var segmentIndex = reader.ReadIndex();
        var offset = recordType == OmfRecordTypes.Ledata32
            ? (int)reader.ReadUInt32()
            : reader.ReadUInt16();
        var dataLength = content.Length - reader.Position;
        var bytes = reader.ReadBytes(dataLength).ToArray();

        if (!segments.TryGetValue(segmentIndex, out var segment))
        {
            segment = new SegmentInfo { SegmentName = $"seg{segmentIndex}", ClassName = string.Empty };
            segments[segmentIndex] = segment;
        }

        segment.Chunks.Add((offset, bytes));
        return (segmentIndex, offset);
    }

    private static (int SegmentIndex, int SegmentOffset) AppendLidata(
        ReadOnlySpan<byte> content,
        byte recordType,
        Dictionary<int, SegmentInfo> segments)
    {
        var use32 = recordType == OmfRecordTypes.Lidata32;
        var reader = new OmfBinaryReader(content);
        var segmentIndex = reader.ReadIndex();
        var offset = use32 ? (int)reader.ReadUInt32() : reader.ReadUInt16();
        var bytes = OmfLidataExpander.ExpandData(content.Slice(reader.Position), use32);

        if (!segments.TryGetValue(segmentIndex, out var segment))
        {
            segment = new SegmentInfo { SegmentName = $"seg{segmentIndex}", ClassName = string.Empty };
            segments[segmentIndex] = segment;
        }

        segment.Chunks.Add((offset, bytes));
        return (segmentIndex, offset);
    }

    private static List<OmfSegmentData> BuildSegmentData(Dictionary<int, SegmentInfo> segments)
    {
        var result = new List<OmfSegmentData>(segments.Count);
        foreach (var (index, info) in segments.OrderBy(static kv => kv.Key))
        {
            if (info.Chunks.Count == 0)
            {
                result.Add(new OmfSegmentData
                {
                    SegmentIndex = index,
                    SegmentName = info.SegmentName,
                    ClassName = info.ClassName,
                    Data = [],
                });
                continue;
            }

            var maxEnd = info.Chunks.Max(static c => c.Offset + c.Bytes.Length);
            var data = new byte[maxEnd];
            foreach (var (offset, bytes) in info.Chunks)
            {
                if (offset + bytes.Length > data.Length)
                {
                    var grown = new byte[offset + bytes.Length];
                    data.AsSpan(0, data.Length).CopyTo(grown);
                    data = grown;
                }

                bytes.CopyTo(data, offset);
            }

            result.Add(new OmfSegmentData
            {
                SegmentIndex = index,
                SegmentName = info.SegmentName,
                ClassName = info.ClassName,
                Data = data,
            });
        }

        return result;
    }

    private static string GetName(List<string> names, int index)
    {
        if (index < 0 || index >= names.Count)
        {
            return string.Empty;
        }

        return names[index];
    }
}

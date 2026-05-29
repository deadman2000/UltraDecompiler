namespace LibParser.Omf;

using LibParser.Models;

/// <summary>Запись FRAME/TARGET thread в FIXUPP.</summary>
internal readonly struct OmfFixuppThreadEntry
{
    public OmfFixuppThreadEntry(int method, int index)
    {
        Method = method;
        Index = index;
    }

    /// <summary>Метод F0–F5 (frame) или базовый T0–T2 (target, 2 бита).</summary>
    public int Method { get; }

    public int Index { get; init; }
}

/// <summary>Состояние FRAME/TARGET threads между записями FIXUPP одного модуля.</summary>
internal sealed class OmfFixuppThreadState
{
    private readonly OmfFixuppThreadEntry[] _target = new OmfFixuppThreadEntry[4];
    private readonly OmfFixuppThreadEntry[] _frame = new OmfFixuppThreadEntry[4];

    public void SetTarget(int threadNumber, OmfFixuppThreadEntry entry) =>
        _target[threadNumber & 3] = entry;

    public void SetFrame(int threadNumber, OmfFixuppThreadEntry entry) =>
        _frame[threadNumber & 3] = entry;

    public OmfFixuppThreadEntry GetTarget(int threadNumber) =>
        _target[threadNumber & 3];

    public OmfFixuppThreadEntry GetFrame(int threadNumber) =>
        _frame[threadNumber & 3];
}

/// <summary>Разбор записи FIXUPP (9Ch/9Dh).</summary>
internal static class OmfFixuppParser
{
    public sealed record ParseResult(
        IReadOnlyList<OmfFixup> Fixups,
        OmfFixuppThreadState Threads);

    public static ParseResult Parse(
        ReadOnlySpan<byte> content,
        byte recordType,
        OmfFixuppThreadState threads,
        int dataSegmentIndex,
        int dataSegmentBaseOffset)
    {
        var use32BitDisp = recordType == OmfRecordTypes.Fixup32;
        var fixups = new List<OmfFixup>();
        var reader = new OmfBinaryReader(content);

        while (!reader.End)
        {
            if (reader.Position >= content.Length)
            {
                break;
            }

            var marker = reader.ReadByte();

            // Нулевые байты в конце записи — padding, не THREAD.
            if (marker == 0 && reader.End)
            {
                break;
            }

            if ((marker & 0x80) == 0)
            {
                if (!TryParseThreadSubrecord(marker, ref reader, threads, content.Length))
                {
                    break;
                }

                continue;
            }

            if (!TryParseFixupSubrecord(
                    marker,
                    ref reader,
                    use32BitDisp,
                    dataSegmentIndex,
                    dataSegmentBaseOffset,
                    content.Length,
                    threads,
                    fixups))
            {
                break;
            }
        }

        return new ParseResult(fixups, threads);
    }

    private static bool TryParseThreadSubrecord(
        byte firstByte,
        ref OmfBinaryReader reader,
        OmfFixuppThreadState threads,
        int contentLength)
    {
        var isFrame = (firstByte & 0x40) != 0;
        var threadNumber = firstByte & 0x03;
        var method = isFrame
            ? (firstByte >> 2) & 0x07
            : (firstByte >> 2) & 0x03;

        var index = 0;
        if (method is 0 or 1 or 2)
        {
            if (!reader.TryReadIndex(out index))
            {
                return false;
            }
        }

        var entry = new OmfFixuppThreadEntry(method, index);
        if (isFrame)
        {
            threads.SetFrame(threadNumber, entry);
        }
        else
        {
            threads.SetTarget(threadNumber, entry);
        }

        return true;
    }

    private static bool TryParseFixupSubrecord(
        byte locLow,
        ref OmfBinaryReader reader,
        bool use32BitDisp,
        int dataSegmentIndex,
        int dataSegmentBaseOffset,
        int contentLength,
        OmfFixuppThreadState threads,
        List<OmfFixup> fixups)
    {
        if (reader.Position >= contentLength)
        {
            return false;
        }

        var locHigh = reader.ReadByte();
        var locationType = (OmfFixupLocationType)((locLow >> 2) & 0x0F);
        var isSegmentRelative = (locLow & 0x40) != 0;
        var dataRecordOffset = ((locLow & 0x03) << 8) | locHigh;

        if (reader.Position >= contentLength)
        {
            return false;
        }

        var fixData = reader.ReadByte();
        var useFrameThread = (fixData & 0x80) != 0;
        var frameField = (fixData >> 4) & 0x07;
        var useTargetThread = (fixData & 0x08) != 0;
        var noDisplacement = (fixData & 0x04) != 0;
        var targetField = fixData & 0x03;

        if (!TryResolveFrame(useFrameThread, frameField, ref reader, threads, contentLength, out var frame))
        {
            return false;
        }

        if (!TryResolveTarget(
                useTargetThread,
                targetField,
                noDisplacement,
                use32BitDisp,
                ref reader,
                threads,
                contentLength,
                out var target))
        {
            return false;
        }

        fixups.Add(new OmfFixup
        {
            SegmentIndex = dataSegmentIndex,
            DataRecordOffset = dataRecordOffset,
            SegmentOffset = dataSegmentBaseOffset + dataRecordOffset,
            LocationType = locationType,
            IsSegmentRelative = isSegmentRelative,
            Frame = frame,
            Target = target,
        });

        return true;
    }

    private static bool TryResolveFrame(
        bool useThread,
        int frameField,
        ref OmfBinaryReader reader,
        OmfFixuppThreadState threads,
        int contentLength,
        out OmfFixupReference frame)
    {
        if (useThread)
        {
            var entry = threads.GetFrame(frameField);
            frame = new OmfFixupReference
            {
                Kind = MapFrameMethod(entry.Method),
                Index = entry.Index,
                FromThread = true,
                ThreadNumber = frameField,
            };
            return true;
        }

        var method = frameField;
        var index = 0;
        if (method is 0 or 1 or 2)
        {
            if (!reader.TryReadIndex(out index))
            {
                frame = null!;
                return false;
            }
        }

        frame = new OmfFixupReference
        {
            Kind = MapFrameMethod(method),
            Index = index,
        };
        return true;
    }

    private static bool TryResolveTarget(
        bool useThread,
        int targetField,
        bool noDisplacement,
        bool use32BitDisp,
        ref OmfBinaryReader reader,
        OmfFixuppThreadState threads,
        int contentLength,
        out OmfFixupReference target)
    {
        int method;
        int index;
        var fromThread = false;
        var threadNumber = 0;

        if (useThread)
        {
            var entry = threads.GetTarget(targetField);
            method = noDisplacement ? entry.Method + 4 : entry.Method;
            index = entry.Index;
            fromThread = true;
            threadNumber = targetField;
        }
        else
        {
            method = (noDisplacement ? 4 : 0) | targetField;
            index = 0;
            if (method is 0 or 1 or 2 or 4 or 5 or 6)
            {
                if (!reader.TryReadIndex(out index))
                {
                    target = null!;
                    return false;
                }
            }
        }

        uint displacement = 0;
        if (!noDisplacement)
        {
            if (use32BitDisp)
            {
                if (!reader.TryReadUInt32(out displacement))
                {
                    target = null!;
                    return false;
                }
            }
            else if (!reader.TryReadUInt16(out var disp16))
            {
                target = null!;
                return false;
            }
            else
            {
                displacement = disp16;
            }
        }

        target = new OmfFixupReference
        {
            Kind = MapTargetMethod(method),
            Index = index,
            Displacement = displacement,
            FromThread = fromThread,
            ThreadNumber = threadNumber,
        };
        return true;
    }

    private static OmfFixupDatumKind MapFrameMethod(int method) => method switch
    {
        0 => OmfFixupDatumKind.Segdef,
        1 => OmfFixupDatumKind.Grpdef,
        2 => OmfFixupDatumKind.Extdef,
        4 => OmfFixupDatumKind.LedataSegment,
        5 => OmfFixupDatumKind.TargetFrame,
        _ => OmfFixupDatumKind.None,
    };

    private static OmfFixupDatumKind MapTargetMethod(int method) => method switch
    {
        0 => OmfFixupDatumKind.Segdef,
        1 => OmfFixupDatumKind.Grpdef,
        2 => OmfFixupDatumKind.Extdef,
        4 => OmfFixupDatumKind.Segdef,
        5 => OmfFixupDatumKind.Grpdef,
        6 => OmfFixupDatumKind.Extdef,
        _ => OmfFixupDatumKind.None,
    };
}

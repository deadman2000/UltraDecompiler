using LibParser.Models;
using UltraDecompiler.Parser;

namespace Tools;

/// <summary>
/// Построение <see cref="RelocationTable"/> для дизассемблера по FIXUPP модуля OMF .LIB.
/// </summary>
internal static class OmfRelocationTableBuilder
{
    /// <summary>
    /// Таблица 16-битных segment-relative релокаций внутри одного сегмента (обычно CODE).
    /// </summary>
    public static RelocationTable Build(OmfSegmentData segment, IReadOnlyList<OmfFixup> fixups)
    {
        var namesByOffset = new Dictionary<int, string>();

        foreach (var fixup in fixups)
        {
            if (fixup.SegmentIndex != segment.SegmentIndex)
            {
                continue;
            }

            if (!fixup.IsSegmentRelative || fixup.LocationType != OmfFixupLocationType.Offset16)
            {
                continue;
            }

            if (fixup.SegmentOffset < 0 || fixup.SegmentOffset > ushort.MaxValue)
            {
                continue;
            }

            var name = GetOffsetName(fixup);
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            namesByOffset[fixup.SegmentOffset] = name;
        }

        var entries = namesByOffset
            .OrderBy(static kv => kv.Key)
            .Select(static kv => new RelocationEntry
            {
                Segment = 0,
                Offset = (ushort)kv.Key,
                OffsetName = kv.Value,
            })
            .ToArray();

        return new RelocationTable("", entries);
    }

    /// <summary>
    /// Имя символической базы смещения: внешний/сегментный символ TARGET, иначе FRAME.
    /// </summary>
    private static string GetOffsetName(OmfFixup fixup)
    {
        if (!string.IsNullOrEmpty(fixup.Target.Name))
        {
            return fixup.Target.Name;
        }

        if (!string.IsNullOrEmpty(fixup.Frame.Name))
        {
            return fixup.Frame.Name;
        }

        return "offset";
    }
}

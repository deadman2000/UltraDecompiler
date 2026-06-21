using LibParser.Models;
using UltraDecompiler.Common;

namespace LibParser.Omf;

/// <summary>
/// Построение <see cref="RelocationTable"/> для дизассемблера по FIXUPP модуля OMF .LIB.
/// </summary>
public static class OmfRelocationTableBuilder
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

            // Offset16: смещения, rel16 у CALL/JMP (pc-rel), PUSH [sym] и т.п.
            // SegmentBase: 16-битная база сегмента (MOV AX, DGROUP перед MOV DS, AX в crt0).
            // Pointer32: far ptr16:16 (CALL/JMP FAR, указатели на функции в crt0).
            if (fixup.LocationType is not (
                OmfFixupLocationType.Offset16
                or OmfFixupLocationType.SegmentBase
                or OmfFixupLocationType.Pointer32))
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
    /// Имя символической цели: внешний/сегментный символ TARGET (+ смещение), иначе FRAME.
    /// </summary>
    private static string GetOffsetName(OmfFixup fixup)
    {
        var name = !string.IsNullOrEmpty(fixup.Target.Name)
            ? fixup.Target.Name
            : fixup.Frame.Name;

        if (string.IsNullOrEmpty(name))
        {
            return "offset";
        }

        if (fixup.Target.Displacement == 0)
        {
            return name;
        }

        return $"{name}+0x{fixup.Target.Displacement:X}";
    }
}

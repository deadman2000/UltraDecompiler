namespace LibParser.Models;

/// <summary>Одна релокация из записи FIXUPP.</summary>
public sealed record OmfFixup
{
    /// <summary>Индекс SEGDEF данных, к которым относится FIXUP (из предшествующей LEDATA).</summary>
    public required int SegmentIndex { get; init; }

    /// <summary>Смещение внутри блока данных LEDATA/LIDATA (10 бит Locat).</summary>
    public required int DataRecordOffset { get; init; }

    /// <summary>Абсолютное смещение в сегменте (LedataOffset + DataRecordOffset).</summary>
    public required int SegmentOffset { get; init; }

    /// <summary>Тип патчируемого поля.</summary>
    public required OmfFixupLocationType LocationType { get; init; }

    /// <summary>M=1: segment-relative; M=0: self-relative (PC-relative).</summary>
    public required bool IsSegmentRelative { get; init; }

    /// <summary>FRAME (база для вычисления адреса).</summary>
    public required OmfFixupReference Frame { get; init; }

    /// <summary>TARGET (цель ссылки).</summary>
    public required OmfFixupReference Target { get; init; }
}

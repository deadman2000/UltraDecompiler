namespace LibParser.Models;

/// <summary>Тип поля, к которому применяется FIXUP (поле Location в Locat).</summary>
public enum OmfFixupLocationType : byte
{
    /// <summary>Младший байт (8-битное смещение).</summary>
    LowByte = 0,

    /// <summary>16-битное смещение.</summary>
    Offset16 = 1,

    /// <summary>16-битная база сегмента (селектор).</summary>
    SegmentBase = 2,

    /// <summary>32-битный far pointer (16:16).</summary>
    Pointer32 = 3,

    /// <summary>Старший байт 16-битного смещения.</summary>
    HighByte = 4,

    /// <summary>16-битное loader-resolved смещение.</summary>
    LoaderOffset16 = 5,

    /// <summary>32-битное смещение.</summary>
    Offset32 = 9,

    /// <summary>48-битный far pointer (16:32).</summary>
    Pointer48 = 11,

    /// <summary>32-битное loader-resolved смещение.</summary>
    LoaderOffset32 = 13,
}

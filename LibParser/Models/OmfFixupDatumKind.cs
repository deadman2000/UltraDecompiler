namespace LibParser.Models;

/// <summary>Способ указания сегмента/группы/символа в FIXUPP.</summary>
public enum OmfFixupDatumKind
{
    /// <summary>Не используется (F4, F5, T4–T6 без индекса).</summary>
    None,

    /// <summary>Индекс SEGDEF.</summary>
    Segdef,

    /// <summary>Индекс GRPDEF.</summary>
    Grpdef,

    /// <summary>Индекс EXTDEF (внешний символ).</summary>
    Extdef,

    /// <summary>F4: сегмент записи LEDATA/LIDATA, к которой относится FIXUP.</summary>
    LedataSegment,

    /// <summary>F5: FRAME определяется через TARGET.</summary>
    TargetFrame,
}

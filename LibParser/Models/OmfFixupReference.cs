namespace LibParser.Models;

/// <summary>Ссылка FRAME или TARGET в FIXUPP.</summary>
public sealed record OmfFixupReference
{
    /// <summary>Способ указания (SEGDEF / EXTDEF / …).</summary>
    public required OmfFixupDatumKind Kind { get; init; }

    /// <summary>Индекс SEGDEF, GRPDEF или EXTDEF (1-based); 0 если не применимо.</summary>
    public required int Index { get; init; }

    /// <summary>Разрешённое имя (EXTDEF, SEGDEF через LNAMES).</summary>
    public string? Name { get; init; }

    /// <summary>Смещение TARGET (0 если поле P=1 в Fix Data).</summary>
    public uint Displacement { get; init; }

    /// <summary>Использовался THREAD вместо явного метода.</summary>
    public bool FromThread { get; init; }

    /// <summary>Номер THREAD (0–3), если <see cref="FromThread"/>.</summary>
    public int ThreadNumber { get; init; }
}

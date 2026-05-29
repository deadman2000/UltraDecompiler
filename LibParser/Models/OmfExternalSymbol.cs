namespace LibParser.Models;

/// <summary>Внешний символ из EXTDEF (индекс совместен с FIXUPP Target Datum).</summary>
public sealed class OmfExternalSymbol
{
    /// <summary>1-based индекс в объединённом списке EXTDEF/LEXTDEF/COMDEF.</summary>
    public required int Index { get; init; }

    public required string Name { get; init; }
}

namespace LibParser.Models;

/// <summary>Публичный символ из записи PUBDEF модуля.</summary>
public sealed class OmfModulePublicSymbol
{
    public required string Name { get; init; }

    /// <summary>Индекс сегмента (как в SEGDEF/PUBDEF).</summary>
    public required int SegmentIndex { get; init; }

    /// <summary>Смещение символа внутри сегмента.</summary>
    public required int Offset { get; init; }
}

namespace LibParser.Models;

/// <summary>Один объектный модуль внутри .LIB.</summary>
public sealed class OmfModule
{
    /// <summary>Имя из THEADR/LHEADR (часто исходный файл).</summary>
    public required string HeaderName { get; init; }

    /// <summary>Имя модуля из COMENT LIBMOD (A3), если есть.</summary>
    public string? LibraryModuleName { get; init; }

    /// <summary>
    /// Номер страницы в файле (как в словаре: FileOffset / PageSize; страница 0 — заголовок F0).
    /// </summary>
    public required ushort PageNumber { get; init; }

    /// <summary>Смещение первой записи модуля в файле.</summary>
    public required int FileOffset { get; init; }

    /// <summary>Сырые байты модуля (включая OMF-записи, до выравнивания по странице).</summary>
    public required byte[] RawData { get; init; }

    /// <summary>Логические сегменты с развёрнутыми данными.</summary>
    public required IReadOnlyList<OmfSegmentData> Segments { get; init; }

    /// <summary>Внешние символы модуля (EXTDEF).</summary>
    public required IReadOnlyList<OmfExternalSymbol> ExternalSymbols { get; init; }

    /// <summary>Релокации FIXUPP (привязаны к предшествующим LEDATA/LIDATA).</summary>
    public required IReadOnlyList<OmfFixup> Fixups { get; init; }

    /// <summary>Публичные символы модуля (PUBDEF).</summary>
    public required IReadOnlyList<OmfModulePublicSymbol> PublicSymbols { get; init; }

    /// <summary>Отображаемое имя: LIBMOD, иначе HeaderName.</summary>
    public string DisplayName => LibraryModuleName ?? HeaderName;

    /// <summary>Кодовые сегменты (класс CODE).</summary>
    public IEnumerable<OmfSegmentData> CodeSegments =>
        Segments.Where(static s => s.IsCode);

    /// <summary>Найти публичный символ модуля по имени (PUBDEF).</summary>
    public OmfModulePublicSymbol? FindPublicSymbol(string symbolName)
    {
        foreach (var symbol in PublicSymbols)
        {
            if (string.Equals(symbol.Name, symbolName, StringComparison.Ordinal))
            {
                return symbol;
            }
        }

        return null;
    }

    /// <summary>Смещение символа в кодовом сегменте или <see langword="null"/>.</summary>
    public int? TryGetCodeOffset(string symbolName)
    {
        var publicSymbol = FindPublicSymbol(symbolName);
        if (publicSymbol is null)
        {
            return null;
        }

        foreach (var segment in Segments)
        {
            if (segment.SegmentIndex == publicSymbol.SegmentIndex && segment.IsCode)
            {
                return publicSymbol.Offset;
            }
        }

        return null;
    }

    public override string ToString() => DisplayName;
}

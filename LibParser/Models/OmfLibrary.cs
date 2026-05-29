namespace LibParser.Models;

/// <summary>Разобранная OMF-библиотека Microsoft QuickC.</summary>
public sealed class OmfLibrary
{
    public required OmfLibraryHeader Header { get; init; }

    public required IReadOnlyList<OmfModule> Modules { get; init; }

    public required IReadOnlyDictionary<string, OmfPublicSymbol> Symbols { get; init; }

    /// <summary>Исходные байты файла.</summary>
    public required byte[] RawData { get; init; }

    /// <summary>Найти модуль по публичному символу.</summary>
    public OmfModule? FindModuleBySymbol(string symbolName)
    {
        if (!Symbols.TryGetValue(symbolName, out var entry))
        {
            return null;
        }

        return GetModuleByPage(entry.ModulePage);
    }

    /// <summary>Модуль по номеру страницы из словаря.</summary>
    public OmfModule? GetModuleByPage(ushort pageNumber)
    {
        foreach (var module in Modules)
        {
            if (module.PageNumber == pageNumber)
            {
                return module;
            }
        }

        // Запасной поиск по смещению (на случай расхождения с выравниванием).
        var expectedOffset = pageNumber * Header.PageSize;
        foreach (var module in Modules)
        {
            if (module.FileOffset == expectedOffset)
            {
                return module;
            }
        }

        return null;
    }
}

namespace LibParser.Models;

/// <summary>Заголовок OMF-библиотеки (запись F0h).</summary>
public sealed class OmfLibraryHeader
{
    /// <summary>Размер страницы библиотеки (RecordLength + 3).</summary>
    public required int PageSize { get; init; }

    /// <summary>Смещение первого байта словаря в файле.</summary>
    public required int DictionaryOffset { get; init; }

    /// <summary>Число 512-байтных блоков словаря.</summary>
    public required ushort DictionaryBlockCount { get; init; }

    /// <summary>Флаги (0x01 — регистрозависимый словарь).</summary>
    public required byte Flags { get; init; }

    /// <summary>Словарь чувствителен к регистру символов.</summary>
    public bool CaseSensitive => (Flags & 0x01) != 0;
}

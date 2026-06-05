using LibParser.Models;

namespace UltraDecompiler.LibMatching;

/// <summary>Результат сопоставления участка EXE с публичным символом OMF-библиотеки.</summary>
public sealed record LibraryMatchInfo
{
    public required string SymbolName { get; init; }

    public required ushort ModulePage { get; init; }

    public required string ModuleName { get; init; }

    public required int ModuleCodeOffset { get; init; }

    /// <summary>Файл OMF-библиотеки, из которой взят символ.</summary>
    public required string LibraryFileName { get; init; }
}

/// <summary>Результат сопоставления точки входа EXE с одной OMF-библиотекой.</summary>
public sealed record EntryPointLibraryMatchInfo
{
    public required OmfLibrary Library { get; init; }

    public required IReadOnlyList<LibraryMatchInfo> Matches { get; init; }

    /// <summary>Совпадение символа <c>__astart</c> модуля crt0 на точке входа.</summary>
    public LibraryMatchInfo? AstartMatch =>
        Matches.FirstOrDefault(static m =>
            m.SymbolName == "__astart"
            && m.ModuleName.Equals("crt0", StringComparison.OrdinalIgnoreCase));
}

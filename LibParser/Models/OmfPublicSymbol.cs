namespace LibParser.Models;

/// <summary>Публичный символ из словаря библиотеки.</summary>
public sealed class OmfPublicSymbol
{
    public required string Name { get; init; }

    /// <summary>Номер страницы модуля (страница 0 — заголовок F0).</summary>
    public required ushort ModulePage { get; init; }
}

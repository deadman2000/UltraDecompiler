namespace LibMatching;

/// <summary>Результат сопоставления участка EXE с публичным символом OMF-библиотеки.</summary>
/// <remarks>
/// Один и тот же модуль может дать несколько записей, если в словаре .LIB
/// несколько имён указывают на одну страницу (например, экспорт и внутренний alias).
/// </remarks>
public sealed record LibraryMatchResult
{
    /// <summary>Имя символа из словаря библиотеки (например <c>_printf</c>).</summary>
    public required string SymbolName { get; init; }

    /// <summary>Страница модуля в .LIB (как в словаре).</summary>
    public required ushort ModulePage { get; init; }

    /// <summary>Отображаемое имя модуля (LIBMOD или THEADR).</summary>
    public required string ModuleName { get; init; }

    /// <summary>Смещение совпавшего тела функции в кодовом сегменте модуля.</summary>
    public required int ModuleCodeOffset { get; init; }
}

using UltraDecompiler.LibMatching;

namespace UltraDecompiler.Decompilation;

/// <summary>Результат полного прогона <see cref="Decompiler"/>.</summary>
public sealed class DecompileResult
{
    public required bool Success { get; init; }

    public int MainOffset { get; init; }

    /// <summary>Подключаемые .LIB: сопоставленные символы по мере декомпиляции (для выбранного варианта).</summary>
    public IReadOnlyList<string> LinkedLibraryFileNames { get; init; } = [];

    /// <summary>
    /// Все возможные варианты подключения библиотек (с учётом взаимозаменяемых crt0-библиотек
    /// и дополняющих их аддонов). Если вариантов несколько — пользователь может выбрать любой.
    /// </summary>
    public IReadOnlyList<LibraryConfiguration> PossibleLibraryConfigurations { get; init; } = [];

    public required ProcedureStorage Procedures { get; init; }

    public required IReadOnlyList<string> OutputFiles { get; init; }

    /// <summary>Восстановленные параметры компиляции.</summary>
    public CompilerOptions CompilerOptions { get; init; } = new();

    public static DecompileResult Failed { get; } = new()
    {
        Success = false,
        Procedures = new ProcedureStorage(),
        OutputFiles = [],
    };
}

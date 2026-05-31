namespace UltraDecompiler.Decompilation;

/// <summary>Результат полного прогона <see cref="Decompiler"/>.</summary>
public sealed class DecompileResult
{
    public required bool Success { get; init; }

    public int MainOffset { get; init; }

    /// <summary>Подключаемые .LIB: сопоставленные символы по мере декомпиляции.</summary>
    public IReadOnlyList<string> LinkedLibraryFileNames { get; init; } = [];

    public required ProcedureStorage Procedures { get; init; }

    public required IReadOnlyList<string> OutputFiles { get; init; }

    public static DecompileResult Failed { get; } = new()
    {
        Success = false,
        Procedures = new ProcedureStorage(),
        OutputFiles = [],
    };
}

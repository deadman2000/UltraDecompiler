namespace LibMatchingTests;

/// <summary>Эталонный EXE, собранный QuickC под разные модели памяти.</summary>
public sealed record ExeMemoryModelCase(
    string Name,
    string ExeFileName,
    string LibraryFileName)
{
    public override string ToString() => Name;
}

/// <summary>Наборы EXE + .LIB для тестов сопоставления EXE.</summary>
internal static class ExeMemoryModelCases
{
    public static IEnumerable<object[]> MemberData =>
        All.Select(static c => new object[] { c });

    public static IReadOnlyList<ExeMemoryModelCase> All { get; } =
    [
        new("Small", "HELLO_S.EXE", "SLIBCE.LIB"),
        new("Compact", "HELLO_C.EXE", "CLIBC.LIB"),
        new("Medium", "HELLO_M.EXE", "MLIBC.LIB"),
        new("Large", "HELLO_L.EXE", "LLIBC.LIB"),
    ];
}

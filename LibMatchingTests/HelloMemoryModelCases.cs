namespace LibMatchingTests;

/// <summary>Эталон hello.c, собранный QuickC под разные модели памяти.</summary>
public sealed record HelloMemoryModelCase(
    string Name,
    string ExeFileName,
    string LibraryFileName)
{
    public override string ToString() => Name;
}

/// <summary>Наборы EXE + .LIB для тестов сопоставления hello.c.</summary>
internal static class HelloMemoryModelCases
{
    public static IEnumerable<object[]> MemberData =>
        All.Select(static c => new object[] { c });

    public static IReadOnlyList<HelloMemoryModelCase> All { get; } =
    [
        new("Small", "HELLO_S.EXE", "SLIBCE.LIB"),
        new("Compact", "HELLO_C.EXE", "CLIBC.LIB"),
        new("Medium", "HELLO_M.EXE", "MLIBC.LIB"),
        new("Large", "HELLO_L.EXE", "LLIBC.LIB"),
    ];
}

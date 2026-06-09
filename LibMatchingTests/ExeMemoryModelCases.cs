using TestSupport;
using UltraDecompiler.Compilation;

namespace LibMatchingTests;

/// <summary>Эталонный EXE, собранный QuickC под разные модели памяти.</summary>
public sealed record ExeMemoryModelCase(
    string Name,
    string SourceFileName,
    MemoryModel MemoryModel,
    string LibraryFileName)
{
    public string ExePath => ExeProvider.Get(SourceFileName, MemoryModel);

    public override string ToString() => Name;
}

/// <summary>Наборы EXE + .LIB для тестов сопоставления EXE.</summary>
internal static class ExeMemoryModelCases
{
    public static IEnumerable<object[]> MemberData =>
        All.Select(static c => new object[] { c });

    public static IReadOnlyList<ExeMemoryModelCase> All { get; } =
    [
        new("Small", "hello.c", MemoryModel.Small, "SLIBCE.LIB"),
        new("Compact", "hello.c", MemoryModel.Compact, "CLIBC.LIB"),
        new("Medium", "hello.c", MemoryModel.Medium, "MLIBC.LIB"),
        new("Large", "hello.c", MemoryModel.Large, "LLIBCE.LIB"),
    ];
}

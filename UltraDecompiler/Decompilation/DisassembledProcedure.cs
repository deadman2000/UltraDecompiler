using UltraDecompiler.LibMatching;

namespace UltraDecompiler.Decompilation;

/// <summary>Дизассемблированная процедура в образе EXE/COM.</summary>
public sealed class DisassembledProcedure
{
    public required int Offset { get; init; }

    public required IReadOnlyList<Instruction> Instructions { get; init; }

    public required string Name { get; init; }

    /// <summary>Функция сопоставлена с символом OMF-библиотеки.</summary>
    public bool IsLibrary { get; init; }

    public LibraryMatchInfo? LibraryMatch { get; init; }

    /// <summary>Сигнатура (тип возврата и параметры) для подстановки в CALL.</summary>
    public ProcedureSignature Signature { get; set; } = ProcedureSignature.Unknown;
}

/// <summary>
/// Хранилище дизассемблированных процедур по смещению в образе.
/// </summary>
public sealed class ProcedureStorage
{
    private readonly Dictionary<int, DisassembledProcedure> _byOffset = [];

    public IReadOnlyCollection<DisassembledProcedure> All => _byOffset.Values;

    public bool Contains(int offset) => _byOffset.ContainsKey(offset);

    public bool TryGet(int offset, out DisassembledProcedure? procedure) =>
        _byOffset.TryGetValue(offset, out procedure);

    public void Add(DisassembledProcedure procedure) =>
        _byOffset[procedure.Offset] = procedure;

    /// <summary>Имя процедуры для подстановки в CALL/JMP или синтетическое <c>sub_XXXX</c>.</summary>
    public string GetName(int offset) =>
        TryGet(offset, out var procedure) ? procedure!.Name : $"sub_{offset:X4}";
}

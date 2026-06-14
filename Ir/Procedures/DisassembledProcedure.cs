using UltraDecompiler.Ir.Builder;
using UltraDecompiler.LibMatching;

namespace UltraDecompiler.Ir.Procedures;

/// <summary>Дизассемблированная процедура в образе EXE/COM.</summary>
public sealed class DisassembledProcedure
{
    public required int Offset { get; init; }

    public required IReadOnlyList<Instruction> Instructions { get; init; }

    /// <summary>
    /// ExpressionBuilder с результатом символического выполнения (IR).
    /// set позволяет обновить после добавления stub в ProcedureStorage (когда стали известны имена всех callee).
    /// </summary>
    public ExpressionBuilder? Expressions { get; set; }

    public required string Name { get; init; }

    /// <summary>Функция сопоставлена с символом OMF-библиотеки.</summary>
    public bool IsLibrary { get; init; }

    public LibraryMatchInfo? LibraryMatch { get; init; }

    /// <summary>Сигнатура (тип возврата и параметры) для подстановки в CALL.</summary>
    public ProcedureSignature Signature { get; set; } = ProcedureSignature.Unknown;

    /// <summary>Имена процедур, вызываемых из тела (после разрешения CallSiteResolver).</summary>
    public IReadOnlyList<string> Callees { get; set; } = [];
}

/// <summary>
/// Хранилище дизассемблированных процедур по смещению в образе.
/// </summary>
public sealed class ProcedureStorage
{
    private readonly Dictionary<int, DisassembledProcedure> _byOffset = [];
    private readonly Dictionary<string, DisassembledProcedure> _byName = new(StringComparer.Ordinal);

    public IReadOnlyCollection<DisassembledProcedure> All => _byOffset.Values;

    public bool Contains(int offset) => _byOffset.ContainsKey(offset);

    public bool TryGet(int offset, out DisassembledProcedure? procedure) =>
        _byOffset.TryGetValue(offset, out procedure);

    public bool TryGetByName(string name, out DisassembledProcedure? procedure) =>
        _byName.TryGetValue(name, out procedure);

    public void Add(DisassembledProcedure procedure)
    {
        _byOffset[procedure.Offset] = procedure;
        _byName[procedure.Name] = procedure;
    }

    /// <summary>Имя процедуры для подстановки в CALL/JMP или синтетическое <c>sub_XXXX</c>.</summary>
    public string GetName(int offset) =>
        TryGet(offset, out var procedure) ? procedure!.Name : $"sub_{offset:X4}";
}

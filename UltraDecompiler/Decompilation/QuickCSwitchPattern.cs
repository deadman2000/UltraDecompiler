namespace UltraDecompiler.Decompilation;

/// <summary>Одна ветка switch, восстановленная из ассемблера QuickC.</summary>
/// <param name="Value">Константа case.</param>
/// <param name="BodyStartOffset">Смещение начала тела case в образе.</param>
public sealed record QuickCSwitchCasePattern(ConstExpr Value, int BodyStartOffset);

/// <summary>
/// Паттерн <c>switch</c> Microsoft QuickC: тела case, затем диспетчер <c>cmp reg, imm; jne; jmp case</c>.
/// </summary>
public sealed class QuickCSwitchPattern
{
    /// <summary>Смещение блока <c>mov reg, [var]; jmp dispatcher</c>.</summary>
    public required int EntryOffset { get; init; }

    /// <summary>Смещение первого <c>cmp reg, imm</c> диспетчера.</summary>
    public required int DispatcherStart { get; init; }

    /// <summary>Общая точка выхода (break) после всех case.</summary>
    public required int MergeOffset { get; init; }

    /// <summary>Регистр, сравниваемый в диспетчере (<c>cmp AX, imm</c> и т.п.).</summary>
    public required GpRegister16 DiscriminantRegister { get; init; }

    /// <summary>Упорядоченные case (без default).</summary>
    public required IReadOnlyList<QuickCSwitchCasePattern> Cases { get; init; }

    /// <summary>Смещение тела default.</summary>
    public required int DefaultBodyOffset { get; init; }

    /// <summary>Блоки диспетчера — помечаются посещёнными при развёртке.</summary>
    public required IReadOnlyList<int> DispatcherBlockOffsets { get; init; }
}

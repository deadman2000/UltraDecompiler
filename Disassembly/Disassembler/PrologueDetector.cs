using UltraDecompiler.Disassembly.Graph;

namespace UltraDecompiler.Disassembler;

/// <summary>
/// Общий детектор стандартного пролога кадра стека QuickC (push bp; mov bp, sp или enter).
/// Используется для восстановления параметров, сигнатур, детекции _chkstk и уровня оптимизации.
/// </summary>
public static class PrologueDetector
{
    /// <summary>
    /// Проверяет наличие типичного пролога QuickC в начале блока: <c>push bp; mov bp, sp</c> или <c>enter</c>.
    /// </summary>
    public static bool HasStandardPrologue(BasicBlock entryBlock)
    {
        if (entryBlock == null)
            return false;

        return HasStandardPrologue(entryBlock.Instructions);
    }

    /// <summary>
    /// Проверяет наличие типичного пролога QuickC по списку инструкций.
    /// </summary>
    public static bool HasStandardPrologue(IReadOnlyList<Instruction> instructions)
    {
        if (instructions == null || instructions.Count == 0)
            return false;

        if (instructions[0].Mnemonic == Mnemonic.ENTER)
            return true;

        for (var i = 0; i < instructions.Count - 1; i++)
        {
            if (IsPushBp(instructions[i]) && IsMovBpSp(instructions[i + 1]))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Возвращает индекс первой инструкции после стандартного пролога (0, если пролога нет).
    /// ENTER даёт 1; push bp + mov bp,sp даёт 2+.
    /// Используется для поиска _chkstk сразу после пролога.
    /// </summary>
    public static int SkipPrologue(IReadOnlyList<Instruction> instructions)
    {
        if (instructions == null || instructions.Count == 0)
            return 0;

        if (instructions[0].Mnemonic == Mnemonic.ENTER)
        {
            return 1;
        }

        for (var i = 0; i < instructions.Count - 1; i++)
        {
            if (IsPushBp(instructions[i]) && IsMovBpSp(instructions[i + 1]))
            {
                return i + 2;
            }
        }

        return 0;
    }

    private static bool IsPushBp(Instruction instruction) =>
        instruction.Mnemonic == Mnemonic.PUSH
        && instruction.Operand1.Type == OperandType.Register16
        && instruction.Operand1.AsGpRegister16() == GpRegister16.BP;

    private static bool IsMovBpSp(Instruction instruction) =>
        instruction.Mnemonic == Mnemonic.MOV
        && instruction.Operand1.Type == OperandType.Register16
        && instruction.Operand1.AsGpRegister16() == GpRegister16.BP
        && instruction.Operand2.Type == OperandType.Register16
        && instruction.Operand2.AsGpRegister16() == GpRegister16.SP;
}

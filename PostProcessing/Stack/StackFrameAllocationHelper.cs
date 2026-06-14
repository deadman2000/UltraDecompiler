namespace UltraDecompiler.PostProcessing.Stack;

/// <summary>Определяет размер локальной области стекового кадра по прологу функции.</summary>
internal static class StackFrameAllocationHelper
{
    public static int? TryGetAllocationSize(IReadOnlyList<Instruction> instructions) =>
        TryGetChkstkAllocationSize(instructions)
        ?? TryGetSubSpAllocationSize(instructions)
        ?? TryGetEnterAllocationSize(instructions);

    private static int? TryGetChkstkAllocationSize(IReadOnlyList<Instruction> instructions)
    {
        for (var i = 0; i < instructions.Count - 1; i++)
        {
            if (instructions[i].Mnemonic != Mnemonic.MOV
                || instructions[i].Operand1.Type != OperandType.Register16
                || instructions[i].Operand1.AsGpRegister16() != GpRegister16.AX
                || instructions[i].Operand2.Type != OperandType.Immediate16)
            {
                continue;
            }

            if (instructions[i + 1].Mnemonic != Mnemonic.CALL)
            {
                continue;
            }

            return instructions[i].Operand2.Value;
        }

        return null;
    }

    /// <summary>QuickC с <c>/Gs</c>: <c>sub sp, N</c> сразу после <c>mov bp, sp</c>.</summary>
    private static int? TryGetSubSpAllocationSize(IReadOnlyList<Instruction> instructions)
    {
        for (var i = 0; i < instructions.Count; i++)
        {
            var instr = instructions[i];
            if (instr.Mnemonic != Mnemonic.SUB
                || instr.Operand1.Type != OperandType.Register16
                || instr.Operand1.AsGpRegister16() != GpRegister16.SP
                || instr.Operand2.Type != OperandType.Immediate16)
            {
                continue;
            }

            if (!HasRecentMovBpSp(instructions, i))
            {
                continue;
            }

            return instr.Operand2.Value;
        }

        return null;
    }

    private static int? TryGetEnterAllocationSize(IReadOnlyList<Instruction> instructions)
    {
        foreach (var instr in instructions)
        {
            if (instr.Mnemonic == Mnemonic.ENTER
                && instr.Operand1.Type == OperandType.Immediate16
                && instr.Operand1.Value > 0)
            {
                return instr.Operand1.Value;
            }
        }

        return null;
    }

    private static bool HasRecentMovBpSp(IReadOnlyList<Instruction> instructions, int subSpIndex)
    {
        for (var i = subSpIndex - 1; i >= 0 && subSpIndex - i <= 4; i--)
        {
            var instr = instructions[i];
            if (instr.Mnemonic == Mnemonic.MOV
                && instr.Operand1.Type == OperandType.Register16
                && instr.Operand1.AsGpRegister16() == GpRegister16.BP
                && instr.Operand2.Type == OperandType.Register16
                && instr.Operand2.AsGpRegister16() == GpRegister16.SP)
            {
                return true;
            }
        }

        return false;
    }
}

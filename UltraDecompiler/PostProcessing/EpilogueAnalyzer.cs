namespace UltraDecompiler.PostProcessing;

/// <summary>Распознавание стандартного эпилога функции QuickC.</summary>
internal static class EpilogueAnalyzer
{
    /// <summary>Инструкция относится к эпилогу (восстановление регистров перед RET).</summary>
    public static bool IsEpilogueInstruction(Instruction instr) =>
        instr.Mnemonic switch
        {
            Mnemonic.POP => true,
            Mnemonic.MOV when instr.Operand1.Type == OperandType.Register16
                && instr.Operand1.AsGpRegister16() == GpRegister16.SP
                && instr.Operand2.Type == OperandType.Register16
                && instr.Operand2.AsGpRegister16() == GpRegister16.BP => true,
            Mnemonic.LEAVE => true,
            Mnemonic.JMP => true,
            _ => false,
        };

    /// <summary>Блок содержит только эпилог и завершается RET.</summary>
    public static bool IsEpilogueTailBlock(IReadOnlyList<Instruction> instructions)
    {
        var hasRet = false;

        foreach (var instr in instructions)
        {
            if (instr.IsReturn)
            {
                hasRet = true;
                continue;
            }

            if (!IsEpilogueInstruction(instr))
            {
                return false;
            }
        }

        return hasRet;
    }
}

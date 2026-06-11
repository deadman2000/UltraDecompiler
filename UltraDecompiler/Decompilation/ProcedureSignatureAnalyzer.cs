namespace UltraDecompiler.Decompilation;

/// <summary>
/// Восстанавливает сигнатуру пользовательской процедуры по ассемблерному телу (пролог, [BP+n], RET).
/// </summary>
public static class ProcedureSignatureAnalyzer
{
    private const int FirstParameterOffset = 4;

    /// <summary>Анализирует инструкции процедуры и строит сигнатуру.</summary>
    public static ProcedureSignature Analyze(DisassembledProcedure procedure)
    {
        var parameters = AnalyzeParameters(procedure.Instructions);
        var returnType = AnalyzeReturnType(procedure.Instructions);
        return new ProcedureSignature(returnType, parameters);
    }

    private static IReadOnlyList<ProcedureParameter> AnalyzeParameters(IReadOnlyList<Instruction> instructions)
    {
        if (!HasStandardPrologue(instructions))
        {
            return [];
        }

        var offsets = CollectParameterOffsets(instructions);
        var result = new List<ProcedureParameter>(offsets.Count);

        foreach (var offset in offsets)
        {
            result.Add(new ProcedureParameter(CType.Int, new StackParameter(offset)));
        }

        return result;
    }

    private static CType AnalyzeReturnType(IReadOnlyList<Instruction> instructions)
    {
        var retIndex = -1;
        for (var i = instructions.Count - 1; i >= 0; i--)
        {
            if (instructions[i].Mnemonic == Mnemonic.RET)
            {
                retIndex = i;
                break;
            }
        }

        if (retIndex < 0)
        {
            return CType.Int;
        }

        // Ищем запись в AX непосредственно перед эпилогом (pop / mov sp,bp / pop bp / ret).
        for (var i = retIndex - 1; i >= 0; i--)
        {
            var instr = instructions[i];
            if (IsEpilogueInstruction(instr))
            {
                continue;
            }

            if (instr.Mnemonic == Mnemonic.JMP)
            {
                continue;
            }

            if (WritesReturnValueToAx(instr))
            {
                return CType.Int;
            }

            break;
        }

        return CType.Void;
    }

    private static bool WritesReturnValueToAx(Instruction instr)
    {
        if (!TargetIsAx(instr.Operand1))
        {
            return false;
        }

        return instr.Mnemonic switch
        {
            Mnemonic.CBW => false,
            Mnemonic.MOV or Mnemonic.XOR or Mnemonic.ADD or Mnemonic.ADC or Mnemonic.SUB or Mnemonic.SBB
                or Mnemonic.AND or Mnemonic.OR or Mnemonic.NOT or Mnemonic.NEG
                or Mnemonic.INC or Mnemonic.DEC
                or Mnemonic.SAL or Mnemonic.SHR or Mnemonic.SAR
                or Mnemonic.ROL or Mnemonic.ROR
                or Mnemonic.POP or Mnemonic.XCHG or Mnemonic.LEA => true,
            _ => false,
        };
    }

    private static bool TargetIsAx(Operand operand) =>
        operand.Type == OperandType.Register16 && operand.AsGpRegister16() == GpRegister16.AX;

    private static bool IsEpilogueInstruction(Instruction instr) =>
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

    private static bool HasStandardPrologue(IReadOnlyList<Instruction> instrs)
    {
        if (instrs.Count == 0)
        {
            return false;
        }

        if (instrs[0].Mnemonic == Mnemonic.ENTER)
        {
            return true;
        }

        for (var i = 0; i < instrs.Count - 1; i++)
        {
            if (IsPushBp(instrs[i]) && IsMovBpSp(instrs[i + 1]))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<int> CollectParameterOffsets(IReadOnlyList<Instruction> instructions)
    {
        var offsets = new SortedSet<int>();

        foreach (var instr in instructions)
        {
            CollectBpOffsets(instr.Operand1, offsets);
            CollectBpOffsets(instr.Operand2, offsets);
        }

        return offsets.ToList();
    }

    // Отлавливаем операции с BP регистром и добавляем смещение в offsets
    private static void CollectBpOffsets(Operand operand, SortedSet<int> offsets)
    {
        if (operand.Type != OperandType.Memory)
            return;

        if (operand.BaseReg != AddressRegister.BP || operand.IndexReg != AddressRegister.None)
            return;

        if (operand.Value < FirstParameterOffset || operand.Value % 2 != 0)
            return;

        offsets.Add(operand.Value);
    }

    private static bool IsPushBp(Instruction instr) =>
        instr.Mnemonic == Mnemonic.PUSH &&
        instr.Operand1.Type == OperandType.Register16 &&
        instr.Operand1.AsGpRegister16() == GpRegister16.BP;

    private static bool IsMovBpSp(Instruction instr) =>
        instr.Mnemonic == Mnemonic.MOV &&
        instr.Operand1.Type == OperandType.Register16 &&
        instr.Operand1.AsGpRegister16() == GpRegister16.BP &&
        instr.Operand2.Type == OperandType.Register16 &&
        instr.Operand2.AsGpRegister16() == GpRegister16.SP;
}

using UltraDecompiler.PostProcessing;

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
            var type = InferParameterType(offset, instructions);
            result.Add(new ProcedureParameter(type, new StackParameter(offset)));
        }

        return result;
    }

    /// <summary>Второй аргумент <c>char</c> часто читают через <c>mov al, [bp+n]</c> или сравнивают как байт.</summary>
    private static CType InferParameterType(int offset, IReadOnlyList<Instruction> instructions)
    {
        if (offset == 6 && UsesByteParameter(instructions, offset))
        {
            return CType.Char;
        }

        return CType.Int;
    }

    private static bool UsesByteParameter(IReadOnlyList<Instruction> instructions, int offset)
    {
        var loadsByteFromStack = false;
        var comparesByteToMemory = false;

        foreach (var instr in instructions)
        {
            if (instr.Mnemonic == Mnemonic.MOV
                && TargetIsAl(instr.Operand1)
                && IsBpByteLoad(instr.Operand2, offset))
            {
                loadsByteFromStack = true;
            }

            if (instr.Mnemonic == Mnemonic.CMP
                && OperandIsAl(instr.Operand2)
                && instr.Operand1.Type == OperandType.Memory)
            {
                comparesByteToMemory = true;
            }
        }

        return loadsByteFromStack && comparesByteToMemory;
    }

    private static bool OperandIsAl(Operand operand) =>
        operand.Type == OperandType.Register8 && operand.AsGpRegister8() == GpRegister8.AL;

    private static bool TargetIsAl(Operand operand) =>
        operand.Type == OperandType.Register8 && operand.AsGpRegister8() == GpRegister8.AL;

    private static bool IsBpByteLoad(Operand operand, int offset) =>
        operand.Type == OperandType.Memory
        && operand.BaseReg == AddressRegister.BP
        && operand.IndexReg == AddressRegister.None
        && operand.Value == offset;

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

            if (ProducesIntReturnValue(instr))
            {
                return CType.Int;
            }

            break;
        }

        return CType.Void;
    }

    private static bool ProducesIntReturnValue(Instruction instr)
    {
        if (instr.Mnemonic is Mnemonic.MUL or Mnemonic.IMUL or Mnemonic.DIV or Mnemonic.IDIV)
        {
            return true;
        }

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
        EpilogueAnalyzer.IsEpilogueInstruction(instr);

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

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
        var clobbers = CollectClobbers(procedure.Instructions);
        return new ProcedureSignature(returnType, parameters, clobbers: clobbers);
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
        var writesAx = false;

        foreach (var instr in instructions)
        {
            if (WritesToAx(instr))
            {
                writesAx = true;
            }
        }

        return writesAx ? CType.Int : CType.Void;
    }

    private static bool WritesToAx(Instruction instr) =>
        instr.Mnemonic switch
        {
            Mnemonic.MOV when TargetIsAx(instr.Operand1) => true,
            Mnemonic.XOR when TargetIsAx(instr.Operand1) => true,
            Mnemonic.ADD or Mnemonic.ADC or Mnemonic.SUB or Mnemonic.SBB
                or Mnemonic.AND or Mnemonic.OR or Mnemonic.XOR
                or Mnemonic.NOT or Mnemonic.NEG
                or Mnemonic.INC or Mnemonic.DEC
                or Mnemonic.SAL or Mnemonic.SHR or Mnemonic.SAR
                or Mnemonic.ROL or Mnemonic.ROR
                or Mnemonic.CBW => TargetIsAx(instr.Operand1),
            Mnemonic.POP when TargetIsAx(instr.Operand1) => true,
            Mnemonic.XCHG when TargetIsAx(instr.Operand1) || TargetIsAx(instr.Operand2) => true,
            Mnemonic.LEA when TargetIsAx(instr.Operand1) => true,
            _ => false,
        };

    private static bool TargetIsAx(Operand operand) =>
        operand.Type == OperandType.Register16 && operand.AsGpRegister16() == GpRegister16.AX;

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
            CollectFromOperand(instr.Operand1, offsets);
            CollectFromOperand(instr.Operand2, offsets);
        }

        return offsets.ToList();
    }

    private static void CollectFromOperand(Operand operand, SortedSet<int> offsets)
    {
        if (operand.Type != OperandType.Memory)
        {
            return;
        }

        if (operand.BaseReg != AddressRegister.BP || operand.IndexReg != AddressRegister.None)
        {
            return;
        }

        if (operand.Value < FirstParameterOffset || operand.Value % 2 != 0)
        {
            return;
        }

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

    /// <summary>
    /// Собирает 16-битные регистры, упоминаемые в инструкциях процедуры (консервативная оценка clobbers).
    /// Используется для пометки регистров, которые callee может изменить (полезно после CALL в CallHandler).
    /// </summary>
    private static IReadOnlySet<GpRegister16> CollectClobbers(IReadOnlyList<Instruction> instructions)
    {
        var regs = new HashSet<GpRegister16>();
        foreach (var instr in instructions)
        {
            CollectReg16(instr.Operand1, regs);
            CollectReg16(instr.Operand2, regs);
        }
        return regs;
    }

    private static void CollectReg16(Operand operand, HashSet<GpRegister16> regs)
    {
        if (operand.Type == OperandType.Register16)
            regs.Add(operand.AsGpRegister16());
    }
}

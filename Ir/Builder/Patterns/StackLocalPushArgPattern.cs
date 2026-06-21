namespace UltraDecompiler.Ir.Builder.Patterns;

/// <summary>
/// QuickC /Ox: после <c>mov [bp-local], reg</c> часто идёт <c>push reg</c> вместо <c>push [local]</c>.
/// Подменяет выражение push на локальную переменную.
/// </summary>
public static class StackLocalPushArgPattern
{
    private const int MaxLookback = 8;

    /// <summary>
    /// Обновляет записи push-аргументов для паттерна mov-local + push-reg (post-pass по блоку).
    /// </summary>
    public static void Apply(ExprBlock block)
    {
        var instructions = block.BasicBlock.Instructions;

        for (var i = 0; i < instructions.Count; i++)
        {
            var instr = instructions[i];
            if (instr.Mnemonic != Mnemonic.PUSH
                || !block.PushExprsByOffset.ContainsKey(instr.Offset))
            {
                continue;
            }

            if (TryResolveRegisterPush(block, instr, out var localExpr))
            {
                block.PushExprsByOffset[instr.Offset] = localExpr;
            }
        }
    }

    /// <summary>
    /// Для <c>push reg</c> после <c>mov [local], reg</c> возвращает локальную переменную.
    /// </summary>
    public static bool TryResolveRegisterPush(ExprBlock block, Instruction pushInstr, out Expr expr)
    {
        expr = null!;

        if (pushInstr.Mnemonic != Mnemonic.PUSH
            || pushInstr.Operand1.Type != OperandType.Register16)
        {
            return false;
        }

        var instructions = block.BasicBlock.Instructions;
        var pushIndex = FindInstructionIndex(instructions, pushInstr.Offset);
        if (pushIndex < 0)
        {
            return false;
        }

        var pushReg = pushInstr.Operand1.AsGpRegister16();
        if (!TryResolvePushExpression(block, instructions, pushIndex, pushReg, out expr))
        {
            return false;
        }

        return true;
    }

    private static bool TryResolvePushExpression(
        ExprBlock block,
        IReadOnlyList<Instruction> instructions,
        int pushIndex,
        GpRegister16 pushReg,
        out Expr expr)
    {
        expr = null!;
        Expr? transformed = null;

        for (var j = pushIndex - 1; j >= 0 && pushIndex - j <= MaxLookback; j--)
        {
            var prev = instructions[j];

            if (prev.Mnemonic is Mnemonic.CALL or Mnemonic.CALL_FAR)
            {
                break;
            }

            if (prev.Mnemonic is Mnemonic.ADD or Mnemonic.SUB
                && prev.Operand1.Type == OperandType.Register16
                && prev.Operand1.AsGpRegister16() == GpRegister16.SP)
            {
                break;
            }

            if (TryParseRegisterMask(prev, pushReg, out var mask))
            {
                transformed = transformed is null
                    ? mask
                    : new Math2Expr(Math2Operation.And, transformed, mask);
                continue;
            }

            if (prev.Mnemonic == Mnemonic.MOV
                && prev.Operand1.Type == OperandType.Memory
                && prev.Operand2.Type == OperandType.Register16
                && prev.Operand2.AsGpRegister16() == pushReg
                && prev.Operand1.BaseReg == AddressRegister.BP
                && prev.Operand1.IndexReg == AddressRegister.None)
            {
                var stackLocal = block.Variables.TryGetStackLocal(prev.Operand1.Value);
                if (stackLocal is null)
                {
                    break;
                }

                expr = transformed is null
                    ? stackLocal.ToGet()
                    : new Math2Expr(Math2Operation.And, stackLocal.ToGet(), transformed);
                return true;
            }

            if (InstructionModifiesRegister(prev, pushReg))
            {
                break;
            }
        }

        return false;
    }

    private static bool TryParseRegisterMask(Instruction instr, GpRegister16 reg, out Expr mask)
    {
        mask = null!;

        if (instr.Operand1.Type != OperandType.Register16
            || instr.Operand1.AsGpRegister16() != reg
            || instr.Operand2.Type != OperandType.Immediate16)
        {
            return false;
        }

        if (instr.Mnemonic == Mnemonic.AND)
        {
            mask = new ConstExpr(instr.Operand2.Value);
            return true;
        }

        return false;
    }

    private static int FindInstructionIndex(IReadOnlyList<Instruction> instructions, int offset)
    {
        for (var i = 0; i < instructions.Count; i++)
        {
            if (instructions[i].Offset == offset)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Инструкция перезаписывает 16-битный GP-регистр (reg — destination).</summary>
    private static bool InstructionModifiesRegister(Instruction instr, GpRegister16 reg)
    {
        if (instr.Operand1.Type != OperandType.Register16
            || instr.Operand1.AsGpRegister16() != reg)
        {
            if (instr.Operand2.Type == OperandType.Register16
                && instr.Operand2.AsGpRegister16() == reg
                && instr.Mnemonic is Mnemonic.XCHG)
            {
                return true;
            }

            return false;
        }

        return instr.Mnemonic is Mnemonic.MOV or Mnemonic.POP or Mnemonic.INC or Mnemonic.DEC
            or Mnemonic.ADD or Mnemonic.SUB or Mnemonic.AND or Mnemonic.OR or Mnemonic.XOR
            or Mnemonic.SAL or Mnemonic.SHR or Mnemonic.SAR or Mnemonic.ROL or Mnemonic.ROR
            or Mnemonic.RCL or Mnemonic.RCR or Mnemonic.NEG or Mnemonic.NOT or Mnemonic.MUL
            or Mnemonic.IMUL or Mnemonic.DIV or Mnemonic.IDIV or Mnemonic.XCHG or Mnemonic.LEA;
    }
}

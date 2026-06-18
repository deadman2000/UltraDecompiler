using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает INC и DEC (специальный случай арифметики на 1).
/// 
/// Важно: на реальном x86 INC/DEC **не затрагивают** флаг CF
/// (в отличие от ADD/SUB 1). Поэтому мы не трогаем CF здесь.
/// 
/// Для операнда, соответствующего именованной переменной (локал/параметр по [BP+disp]
/// или регистр с тем же <see cref="Variable"/>), создаёт <see cref="IncOperation"/> /
/// <see cref="DecOperation"/>. Иначе — <see cref="SetOperation"/> на временную переменную.
/// </summary>
public class IncDecHandler(bool isInc) : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var dst = instr.Operand1;
        var current = dst.GetExpression(block, instr.Segment);

        var op = isInc ? Math2Operation.Add : Math2Operation.Sub;
        Expr result = current.Calculate(op, ConstExpr.One);

        if (dst.Type == OperandType.Memory)
        {
            MemoryIncDecHelper.SnapshotRegistersHoldingStackSlot(block, dst);
        }

        if (TryEmitIncDec(block, dst, instr.Segment, isInc, current))
        {
            block.EndRegisters = UpdateDestination(block, dst, result);
            block.EndRegisters = block.EndRegisters.ApplyArithmeticFlags(result);
            return;
        }

        if (result is not ConstExpr)
        {
            var resultVar = block.Variables.CreateTempVariable();
            block.Operations.Add(new SetOperation(resultVar, result));
            result = resultVar;
        }

        if (dst.Type == OperandType.Register16)
        {
            block.EndRegisters = block.EndRegisters.Set16(dst.AsGpRegister16(), result);
        }
        else if (dst.Type == OperandType.Register8)
        {
            block.EndRegisters = block.EndRegisters.Set8(dst.AsGpRegister8(), result);
        }
        else if (dst.Type == OperandType.Memory)
        {
            dst.EmitStore(block, instr.Segment, result);
        }
        else
        {
            throw new NotImplementedException($"{(isInc ? "INC" : "DEC")} with destination {dst.Type} is not supported");
        }

        block.EndRegisters = block.EndRegisters.ApplyArithmeticFlags(result);
    }

    private static bool TryEmitIncDec(ExprBlock block, Operand dst, Segment segment, bool isInc, Expr current)
    {
        if (dst.Type == OperandType.Memory)
        {
            dst.EmitIncDec(block, segment, isInc);
            return true;
        }

        if (current is Variable variable && !variable.IsInternal && !variable.IsTemp)
        {
            // inc/dec регистра со стековой копией локали — часть Ox mov [local],reg, не var++.
            if (dst.Type is OperandType.Register16 or OperandType.Register8 && variable.IsStack)
            {
                return false;
            }

            block.Operations.Add(isInc ? new IncOperation(variable) : new DecOperation(variable));
            return true;
        }

        return false;
    }

    private static RegisterExpressions UpdateDestination(ExprBlock block, Operand dst, Expr result) =>
        dst.Type switch
        {
            OperandType.Register16 => block.EndRegisters.Set16(dst.AsGpRegister16(), result),
            OperandType.Register8 => block.EndRegisters.Set8(dst.AsGpRegister8(), result),
            _ => block.EndRegisters,
        };
}

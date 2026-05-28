namespace UltraDecompiler.Decompilation.InstructionHandlers;

/// <summary>
/// Обрабатывает INC и DEC (специальный случай арифметики на 1).
/// 
/// Важно: на реальном x86 INC/DEC **не затрагивают** флаг CF
/// (в отличие от ADD/SUB 1). Поэтому мы не трогаем CF здесь.
/// 
/// Результат всегда оборачивается в Variable (через SetOperation), если не константа.
/// </summary>
public class IncDecHandler(bool isInc) : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var dst = instr.Operand1;
        var current = dst.GetExpression(block, instr.Segment);

        var op = isInc ? Math2Operation.Add : Math2Operation.Sub;
        Expr result = current.Calculate(op, ConstExpr.One);

        if (result is not ConstExpr)
        {
            var resultVar = block.Variables.CreateVariable();
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
            var (addr, seg) = dst.BuildMemoryReference(block.EndRegisters, instr.Segment);
            block.Operations.Add(new StoreOperation(addr, seg, result));
        }
        else
        {
            throw new NotImplementedException($"{(isInc ? "INC" : "DEC")} with destination {dst.Type} is not supported");
        }

        block.EndRegisters = block.EndRegisters.ApplyArithmeticFlags(result);
    }
}

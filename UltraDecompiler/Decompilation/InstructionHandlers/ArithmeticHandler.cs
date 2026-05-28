using UltraDecompiler.Disassembler;

namespace UltraDecompiler.Decompilation.InstructionHandlers;

/// <summary>
/// Обрабатывает арифметические инструкции: ADD, SUB, ADC, SBB.
/// 
/// Содержит специальную оптимизацию:
/// - SUB reg, reg → результат всегда 0 (даже если reg содержал Variable)
/// 
/// Для ADC/SBB при известном CF=1 добавляет/вычитает единицу (приближённая модель).
/// CF обновляется более точно, чем просто ApplyArithmeticFlags.
/// </summary>
public class ArithmeticHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var dst = instr.Operand1;
        var srcExpr = instr.Operand2.GetExpression(block, instr.Segment);
        var dstCurrent = dst.GetExpression(block, instr.Segment);

        // Специальная обработка: SUB reg, reg → результат всегда 0
        if (instr.Mnemonic == Mnemonic.SUB && dst.ReferToSameLocation(instr.Operand2))
        {
            if (dst.Type == OperandType.Register16)
                block.EndRegisters = block.EndRegisters.Set16(dst.AsGpRegister16(), ConstExpr.Zero);
            else if (dst.Type == OperandType.Register8)
                block.EndRegisters = block.EndRegisters.Set8(dst.AsGpRegister8(), ConstExpr.Zero);
            else if (dst.Type == OperandType.Memory)
            {
                var (addr, seg) = dst.BuildMemoryReference(block.EndRegisters, instr.Segment);
                block.Operations.Add(new StoreOperation(addr, seg, ConstExpr.Zero));
            }

            block.EndRegisters = block.EndRegisters.ApplyArithmeticFlags(ConstExpr.Zero);

            // Для SUB reg,reg CF тоже должен быть 0
            block.EndRegisters = block.EndRegisters with { CF = ConstExpr.Zero };
            return;
        }

        bool isAdcSbb = instr.Mnemonic == Mnemonic.ADC || instr.Mnemonic == Mnemonic.SBB;
        bool isAddLike = instr.Mnemonic == Mnemonic.ADD || instr.Mnemonic == Mnemonic.ADC;

        var baseOp = isAddLike ? Math2Operation.Add : Math2Operation.Sub;
        Expr result = dstCurrent.Calculate(baseOp, srcExpr);

        // Для ADC/SBB добавляем/вычитаем CF (только если CF — известная константа 0/1)
        if (isAdcSbb)
        {
            Expr carry = ConstExpr.Zero;
            if (block.EndRegisters.CF is ConstExpr cfC && cfC.Value != 0)
                carry = ConstExpr.One;

            if (carry is ConstExpr c && c.Value != 0)
            {
                result = result.Calculate(baseOp, carry);
            }
            // Если CF символический — игнорируем carry (приближённая модель).
        }

        if (result is not ConstExpr)
        {
            var resultVar = block.Variables.CreateVariable();
            block.Operations.Add(new SetOperation(resultVar, result));
            result = resultVar;
        }

        // Обновляем символическое состояние
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
            throw new NotImplementedException($"Arithmetic {instr.Mnemonic} with destination {dst.Type} is not supported");
        }

        block.EndRegisters = block.EndRegisters.ApplyArithmeticFlags(result);

        // Обновление CF для ADD/SUB/ADC/SBB (более точное, чем в ApplyArithmeticFlags)
        if (instr.Mnemonic == Mnemonic.SUB || instr.Mnemonic == Mnemonic.SBB)
        {
            var cfExpr = new CmpExpr(CmpOperation.Ult, dstCurrent, srcExpr);
            block.EndRegisters = block.EndRegisters with { CF = cfExpr };
        }
        else if (instr.Mnemonic == Mnemonic.ADD || instr.Mnemonic == Mnemonic.ADC)
        {
            var cfExpr = new CmpExpr(CmpOperation.Ult, result, dstCurrent);
            block.EndRegisters = block.EndRegisters with { CF = cfExpr };
        }
    }
}

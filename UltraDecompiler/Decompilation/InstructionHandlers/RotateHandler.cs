namespace UltraDecompiler.Decompilation.InstructionHandlers;

/// <summary>
/// Обрабатывает ROL и ROR (простые ротации без использования флага CF).
/// Реализуется через комбинацию сдвигов и побитовых операций.
/// </summary>
public class RotateHandler(bool isLeft) : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var dst = instr.Operand1;
        var countExpr = instr.Operand2.GetExpression(block, instr.Segment);
        var dstCurrent = dst.GetExpression(block, instr.Segment);

        // Для простоты поддерживаем только константное количество бит (самый частый случай в сгенерированном коде)
        if (countExpr is not ConstExpr countConst)
        {
            // Если количество сдвигов динамическое (CL) — пока создаём упрощённую модель
            // Можно улучшить позже
            throw new NotImplementedException("ROL/ROR с динамическим счётчиком (через CL) пока не поддерживается");
        }

        int count = countConst.Value & 0x1F; // x86 маскирует счётчик
        if (count == 0)
        {
            // Ничего не делаем
            return;
        }

        bool is16Bit = dst.Type == OperandType.Register16 ||
                       (dst.Type == OperandType.Memory && instr.Operand1.Value == 16);
        int bitWidth = is16Bit ? 16 : 8;

        // Эмулируем ротацию через сдвиги + or
        // ROL x, n  ==  (x << n) | (x >> (width - n))
        // ROR x, n  ==  (x >> n) | (x << (width - n))

        int shift1 = count;
        int shift2 = bitWidth - count;

        Expr part1, part2;

        if (isLeft)
        {
            part1 = dstCurrent.Calculate(Math2Operation.Shl, new ConstExpr(shift1));
            part2 = dstCurrent.Calculate(Math2Operation.Shr, new ConstExpr(shift2));
        }
        else
        {
            part1 = dstCurrent.Calculate(Math2Operation.Shr, new ConstExpr(shift1));
            part2 = dstCurrent.Calculate(Math2Operation.Shl, new ConstExpr(shift2));
        }

        // Маскируем части, чтобы не вылезали лишние биты при constant folding
        int mask = (1 << bitWidth) - 1;
        Expr masked1 = part1.Calculate(Math2Operation.And, new ConstExpr(mask));
        Expr masked2 = part2.Calculate(Math2Operation.And, new ConstExpr(mask));

        Expr result = masked1.Calculate(Math2Operation.Or, masked2);

        if (result is not ConstExpr)
        {
            var resultVar = block.Variables.CreateVariable();
            block.Operations.Add(new SetOperation(resultVar, result));
            result = resultVar;
        }

        // Записываем результат
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
            throw new NotImplementedException($"Rotate with destination {dst.Type} is not supported");
        }

        // Ротации обновляют флаги (ZF, CF, OF). Для простоты обновляем только ZF.
        // CF для ротаций на 1 бит можно вычислить, но пока упрощаем.
        block.EndRegisters = block.EndRegisters.ApplyArithmeticFlags(result);
    }
}

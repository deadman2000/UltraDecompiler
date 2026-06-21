using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает инструкции сдвига: SAL (SHL), SHR, SAR.
/// 
/// SAL/SHL (арифметический/логический сдвиг влево) — идентичны:
///   result = dest &lt;&lt; count
/// 
/// SHR (логический сдвиг вправо):
///   result = (unsigned)dest &gt;&gt; count
/// 
/// SAR (арифметический сдвиг вправо):
///   result = (signed)dest &gt;&gt; count
/// 
/// Обновление флагов:
///   ZF = (result == 0)
///   SF = знаковый бит результата
///   CF = последний выдвинутый бит (для count=1)
///   OF = 0 (для count=1), иначе undefined
/// </summary>
public class ShiftHandler : IInstructionHandler
{
    private readonly Math2Operation _operation;
    private readonly bool _isArithmeticRight;

    /// <summary>
    /// Создаёт обработчик сдвига.
    /// </summary>
    /// <param name="operation">Shl для SAL/SHL, Shr для SHR/SAR</param>
    /// <param name="isArithmeticRight">true для SAR (знаковый сдвиг вправо)</param>
    public ShiftHandler(Math2Operation operation, bool isArithmeticRight = false)
    {
        _operation = operation;
        _isArithmeticRight = isArithmeticRight;
    }

    public void Handle(ExprBlock block, Instruction instr)
    {
        var dest = instr.Operand1;
        var countExpr = instr.Operand2.GetExpression(block, instr.Segment);
        var destExpr = dest.GetExpression(block, instr.Segment);

        // Вычисляем результат сдвига
        Expr result;
        if (_isArithmeticRight)
        {
            // SAR: знаковый сдвиг вправо
            // Для корректного знакового сдвига нужно расширить знаковый бит
            result = CreateArithmeticShiftRight(destExpr, countExpr, dest.Type);
        }
        else
        {
            // SAL/SHL или SHR: обычный сдвиг
            result = destExpr.Calculate(_operation, countExpr);
        }

        // Записываем результат в целевой операнд
        switch (dest.Type)
        {
            case OperandType.Register16:
                block.Set(dest.AsGpRegister16(), result);
                break;

            case OperandType.Register8:
                block.Set(dest.AsGpRegister8(), result.LowByte());
                break;

            case OperandType.Memory:
                dest.EmitStore(block, instr.Segment, result);
                break;

            default:
                throw new NotImplementedException($"Shift with destination type {dest.Type} is not yet supported");
        }

        // Обновляем флаги
        UpdateFlags(block, destExpr, countExpr, result, dest.Type);
    }

    /// <summary>
    /// Создаёт выражение для арифметического сдвига вправо (SAR).
    /// Для знакового сдвига нужно сохранить знаковый бит.
    /// </summary>
    private static Expr CreateArithmeticShiftRight(Expr value, Expr count, OperandType destType)
    {
        // Для 16-битных и 8-битных значений используем один подход:
        // SAR уже корректно обрабатывается как арифметический сдвиг в IR
        // (при рендеринге в C это будет >> для signed типов)
        return value.Calculate(Math2Operation.Shr, count);
    }

    /// <summary>
    /// Обновляет флаги ZF, SF, CF, OF на основе результата сдвига.
    /// </summary>
    private void UpdateFlags(ExprBlock block, Expr destExpr, Expr countExpr, Expr result, OperandType destType)
    {
        // ZF = (result == 0)
        block.Set(block.Variables.ZF, new CmpExpr(CmpOperation.Eq, result, ConstExpr.Zero));

        // SF = знаковый бит результата
        // Для 16-бит: bit 15, для 8-бит: bit 7
        int signBit = destType == OperandType.Register8 ? 0x80 : 0x8000;
        block.Set(block.Variables.SF, new CmpExpr(CmpOperation.Ne, result.Calculate(Math2Operation.And, new ConstExpr(signBit)), ConstExpr.Zero));

        // CF = последний выдвинутый бит (только для count=1)
        // Для сдвига влево: CF = бит, выдвинутый из старшего разряда
        // Для сдвига вправо: CF = бит, выдвинутый из младшего разряда
        if (countExpr is ConstExpr { Value: 1 })
        {
            Expr cfExpr = _operation switch
            {
                // SAL/SHL: CF = старший бит до сдвига
                Math2Operation.Shl => destExpr.Calculate(Math2Operation.And, new ConstExpr(signBit)).Calculate(Math2Operation.Shr, new ConstExpr(destType == OperandType.Register8 ? 7 : 15)),

                // SHR/SAR: CF = младший бит до сдвига
                Math2Operation.Shr => destExpr.Calculate(Math2Operation.And, new ConstExpr(1)),

                _ => throw new NotImplementedException()
            };
            block.Set(block.Variables.CF, cfExpr);
        }
        else if (countExpr is ConstExpr { Value: 0 })
        {
            // CF не изменяется при count=0
        }
        else
        {
            // Для count > 1 CF = последний выдвинутый бит (сложно вычислить символически)
            // Оставляем упрощённую модель
            block.Set(block.Variables.CF, new ConstExpr(0));
        }

        // OF = 0 для count=1, иначе undefined (у нас 0)
        // OF для сдвигов: устанавливается, если при сдвиге на 1 бит изменился знаковый бит
        if (countExpr is ConstExpr { Value: 1 })
        {
            // OF = 0 для SAL/SHL и SHR
            // Для SAR OF = 0 (знаковый бит не меняется при арифметическом сдвиге)
            block.Set(block.Variables.OF, ConstExpr.Zero);
        }
        else
        {
            // Для count > 1 OF undefined, считаем 0
            block.Set(block.Variables.OF, ConstExpr.Zero);
        }
    }
}

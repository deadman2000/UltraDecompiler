using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает логические инструкции AND, OR, XOR.
/// 
/// Обновляет флаги:
///   ZF = (result == 0)
///   SF = sign bit результата
///   CF = 0
///   OF = 0
/// 
/// Результат записывается в первый операнд (dest).
/// </summary>
public class LogicalHandler : IInstructionHandler
{
    private readonly Math2Operation _operation;

    /// <summary>
    /// Создаёт обработчик логической операции.
    /// </summary>
    /// <param name="operation">Операция (And, Or, Xor)</param>
    public LogicalHandler(Math2Operation operation)
    {
        _operation = operation;
    }

    public void Handle(ExprBlock block, Instruction instr)
    {
        var dest = instr.Operand1;
        var src = instr.Operand2;

        var destExpr = dest.GetExpression(block, instr.Segment);
        var srcExpr = src.GetExpression(block, instr.Segment);

        // Вычисляем результат логической операции
        var result = destExpr.Calculate(_operation, srcExpr);

        // Запоминаем операнды для последующих Jcc (аналогично CmpHandler).
        // AND/TEST/XOR/OR reg,reg или reg,imm обновляют флаги — Jcc может опираться на них.
        // result — уже вычисленное значение (dest & src для AND, dest | src для OR и т.д.).
        block.LastComparisonOperands = (result, ConstExpr.Zero);
        block.LastComparison = instr;

        // Эвристика: AND reg, reg — тождественная операция, результат не меняется.
        // Запись reg = reg & reg избыточна, пропускаем её.
        // Обновляем только флаги.
        var isSelfAnd = _operation == Math2Operation.And
            && AreSameRegister(dest, src, destExpr);

        if (isSelfAnd)
        {
            // Обновляем только флаги, запись результата пропускаем
            block.Set(block.Variables.ZF, new CmpExpr(CmpOperation.Eq, destExpr, ConstExpr.Zero));
            block.Set(block.Variables.SF, new CmpExpr(CmpOperation.Ne, destExpr.Calculate(Math2Operation.And, new ConstExpr(0x8000)), ConstExpr.Zero));
            block.Set(block.Variables.CF, ConstExpr.Zero);
            block.Set(block.Variables.OF, ConstExpr.Zero);
            return;
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
                // Запись в память
                dest.EmitStore(block, instr.Segment, result);
                break;

            default:
                throw new NotImplementedException($"Logical operation with destination type {dest.Type} is not yet supported");
        }

        // Обновляем флаги
        block.Set(block.Variables.ZF, new CmpExpr(CmpOperation.Eq, result, ConstExpr.Zero));
        block.Set(block.Variables.SF, new CmpExpr(CmpOperation.Ne, result.Calculate(Math2Operation.And, new ConstExpr(0x8000)), ConstExpr.Zero));
        block.Set(block.Variables.CF, ConstExpr.Zero);
        block.Set(block.Variables.OF, ConstExpr.Zero);
    }

    /// <summary>
    /// Проверяет, что оба операнда — один и тот же регистр.
    /// </summary>
    private static bool AreSameRegister(Operand dest, Operand src, Expr destExpr)
    {
        if (dest.Type != OperandType.Register16 || src.Type != OperandType.Register16)
        {
            return false;
        }

        var destVar = dest.AsGpRegister16();
        return destVar == src.AsGpRegister16();
    }
}
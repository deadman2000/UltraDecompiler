using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает арифметические инструкции ADD, SUB, ADC, SBB.
/// 
/// Эти инструкции:
/// - Обновляют символическое значение целевого операнда (регистр или память)
/// - Создают SetOperation или StoreOperation с Math2Expr (Add/Sub)
/// - Обновляют флаги (ZF, CF, SF, OF) на основе результата
/// 
/// ADC (Add with Carry) и SBB (Subtract with Borrow) учитывают флаг CF:
/// - ADC: result = left + right + CF
/// - SBB: result = left - right - CF
/// 
/// Обновление флагов:
/// - ZF = (result == 0)
/// - CF = переполнение без знака (для ADD/ADC: unsigned overflow, для SUB/SBB: borrow)
/// - SF = знаковый бит результата
/// - OF = знаковое переполнение
/// </summary>
public class ArithmeticHandler : IInstructionHandler
{
    private readonly Math2Operation _operation;
    private readonly bool _useCarryFlag;

    /// <summary>
    /// Создаёт обработчик арифметической операции.
    /// </summary>
    /// <param name="operation">Базовая операция (Add для ADD/ADC, Sub для SUB/SBB)</param>
    /// <param name="useCarryFlag">true для ADC/SBB (учитывать флаг переноса)</param>
    public ArithmeticHandler(Math2Operation operation, bool useCarryFlag = false)
    {
        _operation = operation;
        _useCarryFlag = useCarryFlag;
    }

    public void Handle(ExprBlock block, Instruction instr)
    {
        var dest = instr.Operand1;
        var src = instr.Operand2;

        var destExpr = dest.GetExpression(block, instr.Segment);
        var srcExpr = src.GetExpression(block, instr.Segment);

        // Вычисляем результат: left + right или left - right
        // Для ADC/SBB добавляем/вычитаем флаг CF
        Expr result;
        if (_useCarryFlag)
        {
            // ADC: result = dest + src + CF
            // SBB: result = dest - src - CF
            var cfExpr = (Expr)block.Variables.CF;
            var carryTerm = _operation == Math2Operation.Add
                ? cfExpr
                : new Math1Expr(Math1Operation.Neg, cfExpr);

            result = destExpr.Calculate(_operation, srcExpr).Calculate(_operation, carryTerm);
        }
        else
        {
            result = destExpr.Calculate(_operation, srcExpr);
        }

        // Записываем результат в целевой операнд
        switch (dest.Type)
        {
            case OperandType.Register16:
                // ADD/SUB/ADC/SBB reg16, reg/mem/imm
                block.Set(dest.AsGpRegister16(), result);
                break;

            case OperandType.Register8:
                // ADD/SUB/ADC/SBB reg8, reg/mem/imm
                block.Set(dest.AsGpRegister8(), result.LowByte());
                break;

            case OperandType.Memory:
                // ADD/SUB/ADC/SBB mem, reg/imm — запись в память
                // Используем TryEmitCompoundAssign для создания AddAssignOperation/SubAssignOperation
                if (!dest.TryEmitCompoundAssign(block, instr.Segment, _operation == Math2Operation.Add, srcExpr, destExpr, out _))
                {
                    // Fallback: обычная запись через StoreOperation
                    dest.EmitStore(block, instr.Segment, result);
                }
                break;

            default:
                throw new NotImplementedException($"Arithmetic with destination type {dest.Type} is not yet supported");
        }

        // Обновляем флаги
        UpdateFlags(block, destExpr, srcExpr, result, _useCarryFlag);
    }

    /// <summary>
    /// Обновляет флаги ZF, CF, SF, OF на основе результата операции.
    /// </summary>
    private void UpdateFlags(ExprBlock block, Expr left, Expr right, Expr result, bool hasCarry)
    {
        // ZF = (result == 0)
        block.Set(block.Variables.ZF, new CmpExpr(CmpOperation.Eq, result, ConstExpr.Zero));

        // SF = знаковый бит результата (result < 0 для знаковой интерпретации)
        // Для 16-битных: SF = bit 15, для 8-битных: SF = bit 7
        // Используем битовую маску: SF = (result & 0x8000) != 0 для 16-бит
        block.Set(block.Variables.SF, new CmpExpr(CmpOperation.Ne, new Math2Expr(Math2Operation.And, result, new ConstExpr(0x8000)), ConstExpr.Zero));

        // CF для ADD/ADC: переполнение без знака
        // CF для SUB/SBB: заём (left < right в беззнаковом смысле)
        Expr cfExpr;
        if (_operation == Math2Operation.Add)
        {
            // ADD: CF = (result < left) unsigned
            // ADC: CF = (result < left) or (result < right) unsigned
            cfExpr = hasCarry
                ? new CmpExpr(CmpOperation.Ult, result, left).BoolOr(new CmpExpr(CmpOperation.Ult, result, right))
                : new CmpExpr(CmpOperation.Ult, result, left);
        }
        else // Sub
        {
            // SUB: CF = (left < right) unsigned
            // SBB: CF = (left < right + CF) unsigned
            cfExpr = hasCarry
                ? new CmpExpr(CmpOperation.Ult, left, new Math2Expr(Math2Operation.Add, right, block.Variables.CF))
                : new CmpExpr(CmpOperation.Ult, left, right);
        }
        block.Set(block.Variables.CF, cfExpr);

        // OF (overflow flag) для знаковых операций
        // ADD: OF = (sign(left) == sign(right)) && (sign(result) != sign(left))
        // SUB: OF = (sign(left) != sign(right)) && (sign(result) != sign(left))
        var leftSign = new Math2Expr(Math2Operation.And, left, new ConstExpr(0x8000));
        var rightSign = new Math2Expr(Math2Operation.And, right, new ConstExpr(0x8000));
        var resultSign = new Math2Expr(Math2Operation.And, result, new ConstExpr(0x8000));

        if (_operation == Math2Operation.Add)
        {
            // OF = (leftSign == rightSign) && (resultSign != leftSign)
            var sameSign = new CmpExpr(CmpOperation.Eq, leftSign, rightSign);
            var diffResultSign = new CmpExpr(CmpOperation.Ne, resultSign, leftSign);
            block.Set(block.Variables.OF, sameSign.BoolAnd(diffResultSign));
        }
        else // Sub
        {
            // OF = (leftSign != rightSign) && (resultSign != leftSign)
            var diffSign = new CmpExpr(CmpOperation.Ne, leftSign, rightSign);
            var diffResultSign = new CmpExpr(CmpOperation.Ne, resultSign, leftSign);
            block.Set(block.Variables.OF, diffSign.BoolAnd(diffResultSign));
        }
    }
}


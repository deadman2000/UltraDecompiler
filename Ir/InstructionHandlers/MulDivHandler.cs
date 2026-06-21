using UltraDecompiler.Disassembly.Disassembler;
using UltraDecompiler.Ir.Expressions;
using UltraDecompiler.Ir.Helpers;
using UltraDecompiler.Ir.Variables;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает инструкции умножения и деления: MUL, IMUL, DIV, IDIV.
/// 
/// Семантика x86:
/// - MUL reg/mem (беззнаковое): AL/AX × operand → AX/(DX:AX)
///   - 8-бит: AL × operand → AX
///   - 16-бит: AX × operand → DX:AX
/// - IMUL reg/mem (знаковое): аналогично MUL, но знаковое умножение
/// - DIV reg/mem (беззнаковое): AX/operand → AL, AH=остаток; (DX:AX)/operand → AX, DX=остаток
/// - IDIV reg/mem (знаковое): аналогично DIV, но знаковое деление
/// 
/// Флаги после MUL/IMUL/DIV/IDIV:
/// - MUL/IMUL: CF и OF установлены, если результат не помещается в младшую половину (AH для 8-бит, DX для 16-бит)
/// - DIV/IDIV: флаги не определены (в IR не моделируем)
/// </summary>
public class MulDivHandler : IInstructionHandler
{
    private readonly Mnemonic _mnemonic;

    public MulDivHandler(Mnemonic mnemonic)
    {
        _mnemonic = mnemonic;
    }

    public void Handle(ExprBlock block, Instruction instr)
    {
        var src = instr.Operand1.GetExpression(block, instr.Segment);
        // Размер определяется типом операнда: Register8/Immediat8 = 8 бит, Register16/Memory = 16 бит
        // Для memory-операнда размер определяется из контекста (анализируется в дизассемблере)
        var is8Bit = instr.Operand1.Type == OperandType.Register8 ||
                     instr.Operand1.Type == OperandType.Immediate8;

        switch (_mnemonic)
        {
            case Mnemonic.MUL:
                if (is8Bit)
                    HandleMul8(block, src);
                else
                    HandleMul16(block, src);
                break;

            case Mnemonic.IMUL:
                if (is8Bit)
                    HandleImul8(block, src);
                else
                    HandleImul16(block, src);
                break;

            case Mnemonic.DIV:
                if (is8Bit)
                    HandleDiv8(block, src, isSigned: false);
                else
                    HandleDiv16(block, src, isSigned: false);
                break;

            case Mnemonic.IDIV:
                if (is8Bit)
                    HandleDiv8(block, src, isSigned: true);
                else
                    HandleDiv16(block, src, isSigned: true);
                break;

            default:
                throw new InvalidOperationException($"Unexpected mnemonic: {_mnemonic}");
        }
    }

    /// <summary>
    /// MUL r/m8: AL × operand → AX
    /// </summary>
    private void HandleMul8(ExprBlock block, Expr src)
    {
        var al = block.Variables.AX.ToGet().Calculate(Math2Operation.And, new ConstExpr(0xFF));
        var result = al.Calculate(Math2Operation.Mul, src);

        // AX = result (полный 16-битный результат)
        block.Set(GpRegister16.AX, result);

        // CF=OF=1, если старший байт результата (AH) не нулевой
        // CF = (result > 0xFF)
        var cf = new CmpExpr(CmpOperation.Ugt, result, new ConstExpr(0xFF));
        block.Set(block.Variables.CF, cf);
        block.Set(block.Variables.OF, cf);
    }

    /// <summary>
    /// MUL r/m16: AX × operand → DX:AX
    /// </summary>
    private void HandleMul16(ExprBlock block, Expr src)
    {
        var ax = block.Variables.AX.ToGet();
        var result = ax.Calculate(Math2Operation.Mul, src);

        // Для 16-битного умножения результат может быть до 32 бит.
        // В IR представляем как 16-битное значение (младшая половина),
        // а старшая половина будет вычислена отдельно.
        // QuickC для 16-битного умножения использует long (32-бит).

        // Создаём временную переменную для полного результата
        var tempResult = block.Variables.CreateTempVariable();
        block.Set(tempResult, result);

        // AX = младшие 16 бит результата
        block.Set(GpRegister16.AX, result);

        // DX = старшие 16 бит (result >> 16)
        // Для упрощения в IR представляем как отдельное вычисление
        var dxValue = result.Calculate(Math2Operation.Shr, new ConstExpr(16));
        block.Set(GpRegister16.DX, dxValue);

        // CF=OF=1, если DX != 0
        block.Set(block.Variables.CF, new CmpExpr(CmpOperation.Ne, dxValue, ConstExpr.Zero));
        block.Set(block.Variables.OF, new CmpExpr(CmpOperation.Ne, dxValue, ConstExpr.Zero));
    }

    /// <summary>
    /// IMUL r/m8: AL × operand → AX (знаковое)
    /// </summary>
    private void HandleImul8(ExprBlock block, Expr src)
    {
        var al = block.Variables.AX.ToGet().Calculate(Math2Operation.And, new ConstExpr(0xFF));
        var result = al.Calculate(Math2Operation.Mul, src);

        // AX = result
        block.Set(GpRegister16.AX, result);

        // CF=OF=1, если результат не помещается в 8 бит со знаком
        // Для 8-бит со знаком: -128 <= result <= 127
        var overflow = new CmpExpr(CmpOperation.Gt, result, new ConstExpr(127))
            .Calculate(Math2Operation.Or, new CmpExpr(CmpOperation.Lt, result, new ConstExpr(-128)));
        block.Set(block.Variables.CF, overflow);
        block.Set(block.Variables.OF, overflow);
    }

    /// <summary>
    /// IMUL r/m16: AX × operand → DX:AX (знаковое)
    /// </summary>
    private void HandleImul16(ExprBlock block, Expr src)
    {
        var ax = block.Variables.AX.ToGet();
        var result = ax.Calculate(Math2Operation.Mul, src);

        // AX = младшие 16 бит
        block.Set(GpRegister16.AX, result);

        // DX = старшие 16 бит
        var dxValue = result.Calculate(Math2Operation.Shr, new ConstExpr(16));
        block.Set(GpRegister16.DX, dxValue);

        // CF=OF=1, если DX != знаковое расширение AX
        // Для положительного AX (бит 15 = 0): DX должен быть 0
        // Для отрицательного AX (бит 15 = 1): DX должен быть 0xFFFF
        // Проверяем: (AX < 0 && DX != 0xFFFF) || (AX >= 0 && DX != 0)
        var axIsNegative = new CmpExpr(CmpOperation.Lt, ax, ConstExpr.Zero);

        // overflow = (axIsNegative && !dxIsFFFF) || (!axIsNegative && !dxIsZero)
        var overflow = axIsNegative.Calculate(Math2Operation.And, new CmpExpr(CmpOperation.Ne, dxValue, new ConstExpr(0xFFFF)))
            .Calculate(Math2Operation.Or,
                new CmpExpr(CmpOperation.Ge, ax, ConstExpr.Zero)
                    .Calculate(Math2Operation.And, new CmpExpr(CmpOperation.Ne, dxValue, ConstExpr.Zero)));

        block.Set(block.Variables.CF, overflow);
        block.Set(block.Variables.OF, overflow);
    }

    /// <summary>
    /// DIV r/m8: AX / operand → AL, AH = остаток (беззнаковое)
    /// </summary>
    private void HandleDiv8(ExprBlock block, Expr src, bool isSigned)
    {
        var ax = block.Variables.AX.ToGet();

        // AL = AX / operand
        var quotient = ax.Calculate(Math2Operation.Div, src);
        var alExpr = quotient.Calculate(Math2Operation.And, new ConstExpr(0xFF));
        block.Set(GpRegister8.AL, alExpr);

        // AH = AX % operand (остаток)
        var remainder = ax.Calculate(Math2Operation.Mod, src);
        var ahExpr = remainder.Calculate(Math2Operation.And, new ConstExpr(0xFF));
        block.Set(GpRegister8.AH, ahExpr);

        // Флаги для DIV/IDIV не определены — не моделируем
    }

    /// <summary>
    /// DIV r/m16: (DX:AX) / operand → AX, DX = остаток (беззнаковое)
    /// </summary>
    private void HandleDiv16(ExprBlock block, Expr src, bool isSigned)
    {
        var ax = block.Variables.AX.ToGet();
        var dx = block.Variables.DX.ToGet();

        // Для 16-битного деления делимое — 32-битное (DX:AX)
        // В IR представляем как составное выражение
        var dividend = dx.Calculate(Math2Operation.Shl, new ConstExpr(16))
            .Calculate(Math2Operation.Or, ax);

        // AX = (DX:AX) / operand
        var quotient = dividend.Calculate(Math2Operation.Div, src);
        block.Set(GpRegister16.AX, quotient);

        // DX = (DX:AX) % operand (остаток)
        var remainder = dividend.Calculate(Math2Operation.Mod, src);
        block.Set(GpRegister16.DX, remainder);

        // Флаги для DIV/IDIV не определены — не моделируем
    }
}


using UltraDecompiler.PostProcessing;

namespace UltraDecompiler.Decompilation.InstructionHandlers;

/// <summary>
/// Обрабатывает инструкции умножения и деления: MUL, IMUL, DIV, IDIV (группа F6/F7).
/// 
/// MUL/IMUL:
///   - Байтовая форма: AH:AL := AL * rm8
///   - Словная форма:  DX:AX := AX * rm16
///   В IR низкая часть (результат в AL/AX) представлена как Mul-выражение,
///   высокая часть — как (mul >> 8/16). Это позволяет отслеживать зависимость DX от умножения.
///   Для IMUL CF/OF моделируются как "high != sign-extend(low)".
/// 
/// DIV/IDIV:
///   - Байт: AL=quot, AH=rem от (AH:AL / rm8)
///   - Слово: AX=quot, DX=rem от (DX:AX / rm16)
///   Dividend моделируется как ((high << n) | low), что даёт читаемый IR и позволяет
///   будущему генератору кода распознать 32-битный делитель.
/// </summary>
public class MulDivHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        bool isMul = instr.Mnemonic is Mnemonic.MUL or Mnemonic.IMUL;
        bool isSigned = instr.Mnemonic is Mnemonic.IMUL or Mnemonic.IDIV;
        bool isByte = instr.Bytes.Length > 0 && instr.Bytes[0] == 0xF6;

        var src = instr.Operand1.GetExpression(block, instr.Segment);

        if (isMul)
        {
            HandleMul(block, instr, src, isByte, isSigned);
        }
        else
        {
            HandleDiv(block, instr, src, isByte, isSigned);
        }
    }

    private static void HandleMul(ExprBlock block, Instruction instr, Expr src, bool isByte, bool isSigned)
    {
        Expr multiplicand = isByte
            ? block.EndRegisters.Get8(GpRegister8.AL)
            : block.EndRegisters.Get16(GpRegister16.AX);

        if (isSigned)
        {
            VariableSignedness.MarkSigned(multiplicand);
            VariableSignedness.MarkSigned(src);
        }
        else
        {
            VariableSignedness.MarkUnsigned(multiplicand);
            VariableSignedness.MarkUnsigned(src);
        }

        Expr product = multiplicand.Calculate(Math2Operation.Mul, src);

        int shift = isByte ? 8 : 16;
        Expr high = product.Calculate(Math2Operation.Shr, new ConstExpr(shift));

        Expr low = product;

        if (low is not ConstExpr)
        {
            var lowV = block.Variables.CreateVariable();
            block.Operations.Add(new SetOperation(lowV, low));
            low = lowV;
        }

        if (high is not ConstExpr)
        {
            var highV = block.Variables.CreateVariable();
            block.Operations.Add(new SetOperation(highV, high));
            high = highV;
        }

        if (isByte)
        {
            block.EndRegisters = block.EndRegisters.Set8(GpRegister8.AL, low);
            block.EndRegisters = block.EndRegisters.Set8(GpRegister8.AH, high);
        }
        else
        {
            block.EndRegisters = block.EndRegisters.Set16(GpRegister16.AX, low);
            block.EndRegisters = block.EndRegisters.Set16(GpRegister16.DX, high);
        }

        if (isSigned)
        {
            VariableSignedness.MarkSigned(low);
            VariableSignedness.MarkSigned(high);
        }
        else
        {
            VariableSignedness.MarkUnsigned(low);
            VariableSignedness.MarkUnsigned(high);
        }

        // CF/OF по правилам x86
        Expr cf;
        if (isSigned)
        {
            // high != sign_extend(low)
            int signShift = isByte ? 7 : 15;
            int maskVal = isByte ? 0xFF : 0xFFFF;
            Expr signMask = new ConstExpr(maskVal);

            Expr signBit = low.Calculate(Math2Operation.Shr, new ConstExpr(signShift))
                              .Calculate(Math2Operation.And, ConstExpr.One);
            Expr minus = ConstExpr.Zero.Calculate(Math2Operation.Sub, signBit);
            Expr expectedHigh = minus.Calculate(Math2Operation.And, signMask);

            if (high is ConstExpr ch && expectedHigh is ConstExpr ce)
            {
                cf = (ch.Value == ce.Value) ? ConstExpr.Zero : ConstExpr.One;
            }
            else
            {
                cf = new CmpExpr(CmpOperation.Ne, high, expectedHigh);
            }
        }
        else
        {
            if (high is ConstExpr ch)
            {
                cf = (ch.Value == 0) ? ConstExpr.Zero : ConstExpr.One;
            }
            else
            {
                cf = new CmpExpr(CmpOperation.Ne, high, ConstExpr.Zero);
            }
        }

        block.EndRegisters = block.EndRegisters with
        {
            CF = cf,
            OF = cf
        };
    }

    private static void HandleDiv(ExprBlock block, Instruction instr, Expr src, bool isByte, bool isSigned)
    {
        Expr highPart = isByte
            ? block.EndRegisters.Get8(GpRegister8.AH)
            : block.EndRegisters.Get16(GpRegister16.DX);
        Expr lowPart = isByte
            ? block.EndRegisters.Get8(GpRegister8.AL)
            : block.EndRegisters.Get16(GpRegister16.AX);

        if (isSigned)
        {
            VariableSignedness.MarkSigned(highPart);
            VariableSignedness.MarkSigned(lowPart);
            VariableSignedness.MarkSigned(src);
        }
        else
        {
            VariableSignedness.MarkUnsigned(highPart);
            VariableSignedness.MarkUnsigned(lowPart);
            VariableSignedness.MarkUnsigned(src);
        }

        int shift = isByte ? 8 : 16;
        // (high << shift) | low  — символическое представление 16/8-битного "wide" дивиденда
        Expr shifted = highPart.Calculate(Math2Operation.Shl, new ConstExpr(shift));
        Expr dividend = shifted.Calculate(Math2Operation.Or, lowPart);

        Expr quot = dividend.Calculate(Math2Operation.Div, src);
        Expr rem = dividend.Calculate(Math2Operation.Mod, src);

        if (quot is not ConstExpr)
        {
            var qv = block.Variables.CreateVariable();
            block.Operations.Add(new SetOperation(qv, quot));
            quot = qv;
        }

        if (rem is not ConstExpr)
        {
            var rv = block.Variables.CreateVariable();
            block.Operations.Add(new SetOperation(rv, rem));
            rem = rv;
        }

        if (isByte)
        {
            block.EndRegisters = block.EndRegisters.Set8(GpRegister8.AL, quot);
            block.EndRegisters = block.EndRegisters.Set8(GpRegister8.AH, rem);
        }
        else
        {
            block.EndRegisters = block.EndRegisters.Set16(GpRegister16.AX, quot);
            block.EndRegisters = block.EndRegisters.Set16(GpRegister16.DX, rem);
        }

        if (isSigned)
        {
            VariableSignedness.MarkSigned(quot);
            VariableSignedness.MarkSigned(rem);
        }
        else
        {
            VariableSignedness.MarkUnsigned(quot);
            VariableSignedness.MarkUnsigned(rem);
        }

        // Для DIV/IDIV флаги не определены — не меняем (оставляем предыдущие значения)
    }
}

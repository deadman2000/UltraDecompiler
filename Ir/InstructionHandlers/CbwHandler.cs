using UltraDecompiler.Ir.Helpers;
using UltraDecompiler.Ir.Variables;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает CBW (Convert Byte to Word).
/// 
/// AL → AX со знаком:
///   если старший бит AL = 1 → AH = 0xFF
///   иначе                   → AH = 0
/// 
/// Используется компилятором после операций с char/byte перед работой с int.
/// </summary>
public class CbwHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var al = block.EndRegisters.Get8(GpRegister8.AL);
        VariableSignedness.MarkChar(al);

        // signBit = (AL >> 7) & 1   → 0 или 1
        Expr signBit = al.Calculate(Math2Operation.And, new ConstExpr(0x80))
                         .Calculate(Math2Operation.Shr, new ConstExpr(7));

        // high = ((0 - signBit) & 0xFF) << 8   → 0x0000 или 0xFF00
        // (0 - 1) даёт -1, -1 & 0xFF = 0xFF (благодаря constant folding)
        Expr minusSign = ConstExpr.Zero.Calculate(Math2Operation.Sub, signBit);
        Expr highByte = minusSign.Calculate(Math2Operation.And, new ConstExpr(0xFF));
        Expr high = highByte.Calculate(Math2Operation.Shl, new ConstExpr(8));

        Expr axValue = high.Calculate(Math2Operation.Or,
            al.Calculate(Math2Operation.And, new ConstExpr(0xFF)));

        block.EndRegisters = block.EndRegisters.Set16(GpRegister16.AX, axValue);
    }
}

namespace UltraDecompiler.Decompilation.InstructionHandlers;

/// <summary>
/// Обрабатывает CWD (Convert Word to Doubleword).
/// 
/// AX → DX:AX со знаком:
///   если старший бит AX = 1 → DX = 0xFFFF
///   иначе                    → DX = 0
/// 
/// Обычно используется перед IDIV/IMUL (для знакового расширения 16→32 бит)
/// или для работы с 32-битными значениями в DX:AX.
/// </summary>
public class CwdHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var ax = block.EndRegisters.Get16(GpRegister16.AX);

        // signBit = (AX >> 15) & 1   → 0 или 1
        // Используем тот же стиль, что и в CbwHandler: And(0x8000) затем Shr(15)
        Expr signBit = ax.Calculate(Math2Operation.And, new ConstExpr(0x8000))
                         .Calculate(Math2Operation.Shr, new ConstExpr(15));

        // dxValue = ((0 - signBit) & 0xFFFF)   → 0x0000 или 0xFFFF
        // (0 - 1) даёт -1, -1 & 0xFFFF = 0xFFFF (благодаря constant folding)
        Expr minusSign = ConstExpr.Zero.Calculate(Math2Operation.Sub, signBit);
        Expr dxValue = minusSign.Calculate(Math2Operation.And, new ConstExpr(0xFFFF));

        block.EndRegisters = block.EndRegisters.Set16(GpRegister16.DX, dxValue);
    }
}

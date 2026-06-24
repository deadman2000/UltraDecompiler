using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает CWD — знаковое расширение AX→DX.
///
/// Семантика x86: если бит 15 AX установлен, DX=0xFFFF, иначе DX=0x0000.
/// Формула: DX = ((AX ^ 0x8000) - 0x8000) >> 16. Флаги не изменяются.
/// </summary>
public class CwdHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var ax = block.Variables.AX.ToGet();
        var dx = ax.Calculate(Math2Operation.Xor, new ConstExpr(0x8000))
            .Calculate(Math2Operation.Sub, new ConstExpr(0x8000))
            .Calculate(Math2Operation.Shr, new ConstExpr(16));
        block.Set(GpRegister16.DX, dx);
    }
}

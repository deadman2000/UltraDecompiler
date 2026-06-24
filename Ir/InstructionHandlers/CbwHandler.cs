using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает CBW — знаковое расширение AL→AX.
///
/// Семантика x86: если бит 7 AL установлен, AH=0xFF, иначе AH=0x00.
/// Формула: AX = (AL ^ 0x80) - 0x80. Флаги не изменяются.
/// </summary>
public class CbwHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var al = block.Variables.AX.ToGet().LowByte();
        var ax = al.Calculate(Math2Operation.Xor, new ConstExpr(0x80))
            .Calculate(Math2Operation.Sub, new ConstExpr(0x80));
        block.Set(GpRegister16.AX, ax);
    }
}

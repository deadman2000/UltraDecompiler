using UltraDecompiler.Disassembler;

namespace UltraDecompiler.Decompilation.InstructionHandlers;

/// <summary>
/// Обрабатывает ENTER imm16, imm8 — создание стекового фрейма.
/// 
/// В большинстве случаев level=0 (генерируется QuickC).
/// Эквивалентно:
///     push bp
///     mov bp, sp
///     sub sp, allocSize
/// 
/// level > 0 почти никогда не используется в коде QuickC 1.0,
/// поэтому пока игнорируем (можно доработать позже при необходимости).
/// </summary>
public class EnterHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        ushort allocSize = (ushort)instr.Operand1.Value;
        byte level = (byte)instr.Operand2.Value;

        // 1. PUSH BP
        var bpValue = block.EndRegisters.Get16(GpRegister16.BP);
        block.EndStack.Push(bpValue);

        // 2. MOV BP, SP
        var currentSp = block.EndRegisters.Get16(GpRegister16.SP);
        block.EndRegisters = block.EndRegisters.Set16(GpRegister16.BP, currentSp);

        // 3. SUB SP, allocSize
        if (allocSize != 0)
        {
            Expr newSp = currentSp.Calculate(Math2Operation.Sub, new ConstExpr(allocSize));
            block.EndRegisters = block.EndRegisters.Set16(GpRegister16.SP, newSp);
        }

        // level > 0 почти никогда не используется в коде QuickC 1.0,
        // поэтому пока игнорируем (можно доработать позже при необходимости).
        if (level > 0)
        {
            // Для level > 0 нужно было бы копировать frame pointers,
            // но это крайне редко встречается в реальном 16-битном коде.
        }
    }
}

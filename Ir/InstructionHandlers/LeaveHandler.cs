using UltraDecompiler.Decompilation;
using UltraDecompiler.Ir.Expressions;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает LEAVE — стандартный эпилог процедуры.
/// 
/// Эквивалентно:
///     mov sp, bp
///     pop bp
/// 
/// LEAVE почти всегда генерируется QuickC в прологе/эпилоге функций с кадром стека.
/// </summary>
public class LeaveHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        // SP ← BP
        var bpValue = block.EndRegisters.Get16(GpRegister16.BP);
        block.EndRegisters = block.EndRegisters.Set16(GpRegister16.SP, bpValue);

        // POP BP: берём значение со стека (если есть)
        if (block.EndStack.Count > 0)
        {
            var value = block.EndStack.Pop();
            block.EndRegisters = block.EndRegisters.Set16(GpRegister16.BP, value);
        }
        else
        {
            // Если стек символически пуст — создаём "неизвестное" значение из памяти
            // (редко, но корректно для анализа)
            var spAddr = block.EndRegisters.Get16(GpRegister16.SP);
            var memVal = new MemExpr(spAddr, block.EndRegisters.GetSegment(CpuSegmentRegister.SS));
            block.EndRegisters = block.EndRegisters.Set16(GpRegister16.BP, memVal);
        }
    }
}

using UltraDecompiler.Disassembler;

namespace UltraDecompiler.Decompilation.InstructionHandlers;

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
        var bpValue = block.EndRegisters.Get16(5); // BP = 5
        block.EndRegisters = block.EndRegisters.Set16(4, bpValue); // SP = 4

        // POP BP: берём значение со стека (если есть)
        if (block.EndStack.Count > 0)
        {
            var value = block.EndStack.Pop();
            block.EndRegisters = block.EndRegisters.Set16(5, value); // BP
        }
        else
        {
            // Если стек символически пуст — создаём "неизвестное" значение из памяти
            // (редко, но корректно для анализа)
            var spAddr = block.EndRegisters.Get16(4);
            var memVal = new MemExpr(spAddr, block.EndRegisters.GetSegment(2)); // SS
            block.EndRegisters = block.EndRegisters.Set16(5, memVal);
        }
    }
}

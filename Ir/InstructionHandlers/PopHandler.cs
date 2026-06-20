using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает POP.
/// 
/// POP снимает значение со стека и записывает его в регистр, сегментный регистр или память.
/// Если стек символически пуст — генерируется ошибка (это почти всегда означает
/// несбалансированные PUSH/POP в исходном коде или очень сложную манипуляцию со стеком).
/// </summary>
public class PopHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var value = block.EndStack.Count == 0
            ? new Variable(Name: "stackErr")
            : block.EndStack.Pop();

        var dst = instr.Operand1;
        if (dst.Type == OperandType.Register16)
        {
            // POP reg16 — просто обновляем символическое значение (аналогично MOV, без SetOperation)
            block.Set(dst.AsGpRegister16(), value);
        }
        else if (dst.Type == OperandType.SegmentRegister)
        {
            // POP ES / SS / DS (CS через POP невозможен на 8086)
            block.Set(dst.AsCpuSegmentRegister(), value);
        }
        else if (dst.Type == OperandType.Memory)
        {
            // POP WORD PTR [addr] — для локалов на стеке создаём SetOperation
            dst.EmitStore(block, instr.Segment, value);
        }
        else
        {
            throw new NotImplementedException($"POP into {dst.Type} is not supported");
        }
    }
}

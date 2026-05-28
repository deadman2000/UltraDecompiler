namespace UltraDecompiler.Decompilation.InstructionHandlers;

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
        if (block.EndStack.Count == 0)
        {
            throw new InvalidOperationException(
                $"POP at offset {instr.Offset:X4} from empty symbolic stack (unbalanced PUSH/POP or indirect manipulation of SP)");
        }

        var value = block.EndStack.Pop();

        var dst = instr.Operand1;
        if (dst.Type == OperandType.Register16)
        {
            // POP reg16 — просто обновляем символическое значение (аналогично MOV, без SetOperation)
            block.EndRegisters = block.EndRegisters.Set16(dst.AsGpRegister16(), value);
        }
        else if (dst.Type == OperandType.SegmentRegister)
        {
            // POP ES / SS / DS (CS через POP невозможен на 8086)
            block.EndRegisters = block.EndRegisters.SetSegment(dst.AsCpuSegmentRegister(), value);
        }
        else if (dst.Type == OperandType.Memory)
        {
            // POP WORD PTR [addr] — создаём StoreOperation
            var (addr, seg) = dst.BuildMemoryReference(block.EndRegisters, instr.Segment);
            block.Operations.Add(new StoreOperation(addr, seg, value));
        }
        else
        {
            throw new NotImplementedException($"POP into {dst.Type} is not supported");
        }
    }
}

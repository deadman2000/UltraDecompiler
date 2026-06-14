using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает MOV (пересылка данных).
/// 
/// Особенность MOV в этом декомпиляторе:
/// - MOV обычно НЕ создаёт SetOperation.
/// - Он просто обновляет символическое значение в RegisterExpressions.
/// - Это отражает семантику QuickC: MOV — это в первую очередь передача значения,
///   а не "вычисление".
/// 
/// Исключение — MOV в память: в этом случае создаётся StoreOperation,
/// потому что это реальная запись в память программы.
/// </summary>
public class MovHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var exprSrc = instr.Operand2.GetExpression(block, instr.Segment);

        // Обновляем символическое значение регистра-назначения.
        // Сама операция MOV обычно не порождает отдельный SetOperation —
        // она просто "передаёт" выражение дальше.
        if (instr.Operand1.Type == OperandType.Register16)
        {
            block.EndRegisters = block.EndRegisters.Set16(instr.Operand1.AsGpRegister16(), exprSrc);
        }
        else if (instr.Operand1.Type == OperandType.Register8)
        {
            block.EndRegisters = block.EndRegisters.Set8(instr.Operand1.AsGpRegister8(), exprSrc);
        }
        else if (instr.Operand1.Type == OperandType.SegmentRegister)
        {
            block.EndRegisters = block.EndRegisters.SetSegment(instr.Operand1.AsCpuSegmentRegister(), exprSrc);
        }
        else if (instr.Operand1.Type == OperandType.Memory)
        {
            instr.Operand1.EmitStore(block, instr.Segment, exprSrc);
        }
        else
        {
            throw new NotImplementedException($"MOV with destination {instr.Operand1.Type} is not supported");
        }
    }
}

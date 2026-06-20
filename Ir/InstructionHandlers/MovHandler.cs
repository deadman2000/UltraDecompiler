using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает MOV (пересылка данных).
/// 
/// MOV обновляет символическое значение целевого операнда:
/// - MOV reg, reg/mem/imm — создаёт SetOperation
/// - MOV mem, reg/imm — создаёт StoreOperation (запись в память)
/// - MOV seg, reg/mem — обновляет сегментный регистр
/// </summary>
public class MovHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var src = instr.Operand2.GetExpression(block, instr.Segment);

        switch (instr.Operand1.Type)
        {
            case OperandType.Register16:
                // MOV reg16, reg/mem/imm
                block.Set(instr.Operand1.AsGpRegister16(), src);
                break;

            case OperandType.Register8:
                // MOV reg8, reg/mem/imm
                block.Set(instr.Operand1.AsGpRegister8(), src.LowByte());
                break;

            case OperandType.SegmentRegister:
                // MOV seg, reg/mem
                block.Set(instr.Operand1.AsCpuSegmentRegister(), src);
                break;

            case OperandType.Memory:
                // MOV mem, reg/imm — запись в память
                instr.Operand1.EmitStore(block, instr.Segment, src);
                break;

            default:
                throw new NotImplementedException($"MOV with destination type {instr.Operand1.Type} is not yet supported");
        }
    }
}

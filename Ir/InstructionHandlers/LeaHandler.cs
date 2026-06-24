using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает LEA (загрузка эффективного адреса).
/// В отличие от MOV, не обращается к памяти — вычисляет только offset-часть адреса.
/// </summary>
public class LeaHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        Expr src = instr.Operand2.Type switch
        {
            OperandType.Memory => instr.Operand2.GetEffectiveAddress(block, instr.Segment),
            OperandType.Register16 => instr.Operand2.GetExpression(block, instr.Segment),
            _ => throw new NotImplementedException($"LEA with source type {instr.Operand2.Type} is not yet supported")
        };

        block.Set(instr.Operand1.AsGpRegister16(), src);
    }
}

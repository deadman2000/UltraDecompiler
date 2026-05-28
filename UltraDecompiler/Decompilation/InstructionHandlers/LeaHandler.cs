namespace UltraDecompiler.Decompilation.InstructionHandlers;

/// <summary>
/// Обрабатывает LEA (Load Effective Address).
/// 
/// LEA — особенная инструкция: она вычисляет адрес, но НЕ разыменовывает память.
/// Используется компилятором очень часто для сложных вычислений адресов
/// (особенно с BP как базой кадра стека).
/// 
/// В отличие от MOV reg, [mem], LEA не создаёт MemExpr — только адресное выражение.
/// </summary>
public class LeaHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        if (instr.Operand1.Type == OperandType.Register16)
        {
            // LEA загружает эффективный адрес (не разыменовывает память).
            Expr eaExpr = instr.Operand2.Type == OperandType.Memory
                ? instr.Operand2.GetEffectiveAddress(block.EndRegisters, instr.Segment)
                : instr.Operand2.GetExpression(block, instr.Segment);

            block.EndRegisters = block.EndRegisters.Set16(instr.Operand1.AsGpRegister16(), eaExpr);
        }
        else
        {
            throw new NotImplementedException($"LEA with destination {instr.Operand1.Type} is not supported");
        }
    }
}

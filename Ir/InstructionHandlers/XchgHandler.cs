using UltraDecompiler.Decompilation;
using UltraDecompiler.Ir.Expressions;
using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает XCHG — обмен значений между двумя операндами.
/// 
/// Поддерживает reg/reg и reg/mem формы (самые распространённые в коде QuickC).
/// Для памяти: читаем старое значение, пишем в память старое значение регистра,
/// а в регистр — старое значение из памяти.
/// </summary>
public class XchgHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var op1 = instr.Operand1;
        var op2 = instr.Operand2;

        Expr val1 = op1.GetExpression(block, instr.Segment);
        Expr val2 = op2.GetExpression(block, instr.Segment);

        // Обновляем первый операнд значением второго
        if (op1.Type == OperandType.Register16)
        {
            block.EndRegisters = block.EndRegisters.Set16(op1.AsGpRegister16(), val2);
        }
        else if (op1.Type == OperandType.Register8)
        {
            block.EndRegisters = block.EndRegisters.Set8(op1.AsGpRegister8(), val2);
        }
        else if (op1.Type == OperandType.Memory)
        {
            op1.EmitStore(block, instr.Segment, val2);
        }

        // Обновляем второй операнд значением первого (симметрично)
        if (op2.Type == OperandType.Register16)
        {
            block.EndRegisters = block.EndRegisters.Set16(op2.AsGpRegister16(), val1);
        }
        else if (op2.Type == OperandType.Register8)
        {
            block.EndRegisters = block.EndRegisters.Set8(op2.AsGpRegister8(), val1);
        }
        else if (op2.Type == OperandType.Memory)
        {
            op2.EmitStore(block, instr.Segment, val1);
        }
    }
}

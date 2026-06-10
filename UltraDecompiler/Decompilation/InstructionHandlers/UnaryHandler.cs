using UltraDecompiler.PostProcessing;

namespace UltraDecompiler.Decompilation.InstructionHandlers;

/// <summary>
/// Обрабатывает унарные операции: NOT и NEG.
/// 
/// NOT — побитовое отрицание (~).
/// NEG — арифметическое отрицание (-x, с учётом переполнения для MIN_VALUE).
/// 
/// Результат всегда оборачивается в новую Variable через SetOperation.
/// </summary>
public class UnaryHandler(Math1Operation operation) : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var dst = instr.Operand1;
        var current = dst.GetExpression(block, instr.Segment);

        if (operation == Math1Operation.Neg)
        {
            VariableSignedness.MarkSigned(current);
        }

        Expr result = current.Calculate(operation);

        if (result is not ConstExpr)
        {
            var resultVar = block.Variables.CreateVariable();
            block.Operations.Add(new SetOperation(resultVar, result));
            result = resultVar;
        }

        if (operation == Math1Operation.Neg)
        {
            VariableSignedness.MarkSigned(result);
        }

        if (dst.Type == OperandType.Register16)
        {
            block.EndRegisters = block.EndRegisters.Set16(dst.AsGpRegister16(), result);
        }
        else if (dst.Type == OperandType.Register8)
        {
            block.EndRegisters = block.EndRegisters.Set8(dst.AsGpRegister8(), result);
        }
        else if (dst.Type == OperandType.Memory)
        {
            dst.EmitStore(block, instr.Segment, result);
        }
        else
        {
            throw new NotImplementedException($"{operation} with destination {dst.Type} is not supported");
        }

        block.EndRegisters = block.EndRegisters.ApplyArithmeticFlags(result);
    }
}

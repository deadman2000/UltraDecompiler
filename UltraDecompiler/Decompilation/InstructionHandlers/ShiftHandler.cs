using UltraDecompiler.PostProcessing;

namespace UltraDecompiler.Decompilation.InstructionHandlers;

/// <summary>
/// Обрабатывает сдвиги: SAL/SHL, SHR, SAR.
/// 
/// Второй операнд (количество бит) может быть:
/// - непосредственным значением (1, 3, 5 и т.д.)
/// - регистром CL (динамический сдвиг)
/// 
/// Сейчас SAR трактуется как SHR — это упрощение (QuickC редко использует арифметический сдвиг
/// в местах, критичных для знака).
/// </summary>
public class ShiftHandler(Math2Operation shiftOp, bool? signedShift = null) : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var dst = instr.Operand1;

        // Второй операнд сдвига — обычно константа или CL.
        // GetExpression корректно обработает и то, и другое.
        var srcExpr = instr.Operand2.GetExpression(block, instr.Segment);
        var dstCurrent = dst.GetExpression(block, instr.Segment);

        if (signedShift == true)
        {
            VariableSignedness.MarkSigned(dstCurrent);
        }
        else if (signedShift == false)
        {
            VariableSignedness.MarkUnsigned(dstCurrent);
        }

        Expr result = dstCurrent.Calculate(shiftOp, srcExpr);

        if (result is not ConstExpr)
        {
            var resultVar = block.Variables.CreateVariable();
            block.Operations.Add(new SetOperation(resultVar, result));
            result = resultVar;
        }

        if (signedShift == true)
        {
            VariableSignedness.MarkSigned(result);
        }
        else if (signedShift == false)
        {
            VariableSignedness.MarkUnsigned(result);
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
            throw new NotImplementedException($"Shift {shiftOp} with destination {dst.Type} is not supported");
        }

        block.EndRegisters = block.EndRegisters.ApplyArithmeticFlags(result);
    }
}

using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает CMP (сравнение).
/// </summary>
public class CmpHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var left = instr.Operand1.GetExpression(block, instr.Segment);
        var right = instr.Operand2.GetExpression(block, instr.Segment);

        block.LastComparisonOperands = (left, right);
        block.LastComparison = instr;

        block.Set(block.Variables.ZF, new CmpExpr(CmpOperation.Eq, left, right));
        block.Set(block.Variables.CF, new CmpExpr(CmpOperation.Ult, left, right)); // left &lt; right (unsigned) → CF
    }
}

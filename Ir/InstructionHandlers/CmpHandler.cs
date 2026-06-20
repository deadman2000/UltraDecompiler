using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает CMP (сравнение).
/// 
/// CMP не создаёт SetOperation (результат не записывается),
/// но обновляет символические значения флагов в RegisterExpressions:
/// 
/// - ZF = (left == right)
/// - CF = (left u&lt; right)   ← беззнаковое "меньше", соответствует биту переноса (borrow)
/// 
/// Это позволяет корректно строить условия для JAE/JB/JA/JBE и т.д.
/// </summary>
public class CmpHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var left = instr.Operand1.GetExpression(block, instr.Segment);
        var right = instr.Operand2.GetExpression(block, instr.Segment);

        block.LastComparisonOperands = (left, right);

        block.Set(block.Variables.ZF, new CmpExpr(CmpOperation.Eq, left, right));
        block.Set(block.Variables.CF, new CmpExpr(CmpOperation.Ult, left, right)); // left &lt; right (unsigned) → CF
    }
}

namespace UltraDecompiler.Decompilation.InstructionHandlers;

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

        var zfExpr = new CmpExpr(CmpOperation.Eq, left, right);
        var cfExpr = new CmpExpr(CmpOperation.Ult, left, right); // left &lt; right (unsigned) → CF

        block.EndRegisters = block.EndRegisters with
        {
            ZF = zfExpr,
            CF = cfExpr
            // SF и OF можно добавить позже при необходимости (для знаковых Jcc)
        };
    }
}

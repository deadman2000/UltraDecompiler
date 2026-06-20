using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает TEST (побитовое И без записи результата).
/// 
/// Обновляет флаги:
///   ZF = ((left &amp; right) == 0)
///   CF = 0
///   OF = 0
/// 
/// Не порождает Operation (аналогично CMP).
/// </summary>
public class TestHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var left = instr.Operand1.GetExpression(block, instr.Segment);
        var right = instr.Operand2.GetExpression(block, instr.Segment);

        var andExpr = left.Calculate(Math2Operation.And, right);
        block.Set(block.Variables.ZF, new CmpExpr(CmpOperation.Eq, andExpr, ConstExpr.Zero));
        block.Set(block.Variables.CF, ConstExpr.Zero);
        block.Set(block.Variables.OF, ConstExpr.Zero);
    }
}

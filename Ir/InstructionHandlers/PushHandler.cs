using UltraDecompiler.Ir.Builder.Patterns;
using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает PUSH.
/// 
/// PUSH просто кладёт вычисленное значение операнда на символический стек.
/// Никаких SetOperation не создаётся — это чисто стековая операция.
/// </summary>
public class PushHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var expr = ResolvePushExpression(block, instr);
        block.PushExprsByOffset[instr.Offset] = expr;
        block.EndStack.Push(expr);
    }

    private static Expr ResolvePushExpression(ExprBlock block, Instruction instr)
    {
        if (instr.Operand1.Type == OperandType.Register16
            && StackLocalPushArgPattern.TryResolveRegisterPush(block, instr, out var localExpr))
        {
            return localExpr;
        }

        return instr.Operand1.GetExpression(block, instr.Segment);
    }
}

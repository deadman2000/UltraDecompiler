using UltraDecompiler.Ir.Expressions;
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
        var expr = instr.Operand1.GetExpression(block, instr.Segment);
        block.PushExprsByOffset[instr.Offset] = expr;
        block.EndStack.Push(expr);
    }
}

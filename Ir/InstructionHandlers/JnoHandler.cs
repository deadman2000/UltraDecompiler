namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обработчик JNO (Jump if Not Overflow) — переход при OF=0.
/// </summary>
public class JnoHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return !block.Variables.OF.ToGet();
    }
}

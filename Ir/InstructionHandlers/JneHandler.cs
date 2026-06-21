namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обработчик JNE (Jump if Not Equal) — переход при ZF=0.
/// </summary>
public class JneHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return !block.Variables.ZF.ToGet();
    }
}

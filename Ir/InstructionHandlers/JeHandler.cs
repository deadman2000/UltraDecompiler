namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обработчик JE (Jump if Equal) — переход при ZF=1.
/// </summary>
public class JeHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return block.Variables.ZF.ToGet();
    }
}

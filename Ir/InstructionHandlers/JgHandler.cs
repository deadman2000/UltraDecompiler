namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обработчик JG (Jump if Greater) — переход при ZF=0 и SF==OF (знаковое больше).
/// </summary>
public class JgHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return !block.Variables.ZF & !(block.Variables.SF ^ block.Variables.OF);
    }
}

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обработчик JGE (Jump if Greater or Equal) — переход при SF==OF (знаковое больше или равно).
/// </summary>
public class JgeHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return !(block.Variables.SF.ToGet() ^ block.Variables.OF.ToGet());
    }
}

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обработчик JL (Jump if Less) — переход при SF!=OF (знаковое меньше).
/// </summary>
public class JlHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return block.Variables.SF ^ block.Variables.OF;
    }
}

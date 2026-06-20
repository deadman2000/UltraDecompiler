namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обработчик JO (Jump if Overflow) — переход при OF=1.
/// </summary>
public class JoHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return block.Variables.OF;
    }
}

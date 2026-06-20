namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обработчик JB (Jump if Below) — переход при CF=1 (беззнаковое меньше).
/// </summary>
public class JbHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return block.Variables.CF;
    }
}

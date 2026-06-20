namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обработчик JAE (Jump if Above or Equal) — переход при CF=0 (беззнаковое больше или равно).
/// </summary>
public class JaeHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return !block.Variables.CF;
    }
}

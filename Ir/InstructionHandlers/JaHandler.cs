namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обработчик JA (Jump if Above) — переход при CF=0 и ZF=0 (беззнаковое больше).
/// </summary>
public class JaHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return !block.Variables.CF & !block.Variables.ZF;
    }
}

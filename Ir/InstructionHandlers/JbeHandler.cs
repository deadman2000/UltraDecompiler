namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обработчик JBE (Jump if Below or Equal) — переход при CF=1 или ZF=1.
/// </summary>
public class JbeHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return block.Variables.CF | block.Variables.ZF;
    }
}

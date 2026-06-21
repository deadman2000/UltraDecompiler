namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обработчик JLE (Jump if Less or Equal) — переход при ZF=1 или SF!=OF.
/// </summary>
public class JleHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return block.Variables.ZF.ToGet() | (block.Variables.SF.ToGet() ^ block.Variables.OF.ToGet());
    }
}

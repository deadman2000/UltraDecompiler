namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обработчик JCXZ (Jump if CX is Zero) — переход при CX=0.
/// </summary>
public class JcxzHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return new CmpExpr(CmpOperation.Eq, block.Variables.CX.ToGet(), ConstExpr.Zero);
    }
}

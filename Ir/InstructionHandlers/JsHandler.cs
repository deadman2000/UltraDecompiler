namespace UltraDecompiler.Ir.InstructionHandlers;

public class JsHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return block.EndRegisters.SF;
    }
}

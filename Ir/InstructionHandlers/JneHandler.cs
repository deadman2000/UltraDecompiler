namespace UltraDecompiler.Ir.InstructionHandlers;

public class JneHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return !block.EndRegisters.ZF;
    }
}

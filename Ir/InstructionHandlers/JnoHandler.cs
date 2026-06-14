namespace UltraDecompiler.Ir.InstructionHandlers;

public class JnoHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return !block.EndRegisters.OF;
    }
}

namespace UltraDecompiler.Ir.InstructionHandlers;

public class JoHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return block.EndRegisters.OF;
    }
}

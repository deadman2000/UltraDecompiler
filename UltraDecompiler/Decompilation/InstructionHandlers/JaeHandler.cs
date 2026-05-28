namespace UltraDecompiler.Decompilation.InstructionHandlers;

public class JaeHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return !block.EndRegisters.CF;
    }
}

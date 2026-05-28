namespace UltraDecompiler.Decompilation.InstructionHandlers;

public class JeHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return block.EndRegisters.ZF;
    }
}

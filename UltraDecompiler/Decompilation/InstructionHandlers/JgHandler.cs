namespace UltraDecompiler.Decompilation.InstructionHandlers;

public class JgHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return !block.EndRegisters.ZF & block.EndRegisters.SfEqOf();
    }
}

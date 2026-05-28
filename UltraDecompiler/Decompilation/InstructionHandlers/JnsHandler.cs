namespace UltraDecompiler.Decompilation.InstructionHandlers;

public class JnsHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return !block.EndRegisters.SF;
    }
}

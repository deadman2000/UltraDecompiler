namespace UltraDecompiler.Decompilation.InstructionHandlers;

public class JlHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return block.EndRegisters.SfNeOf();
    }
}

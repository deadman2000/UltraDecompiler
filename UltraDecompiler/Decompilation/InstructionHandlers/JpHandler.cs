namespace UltraDecompiler.Decompilation.InstructionHandlers;

public class JpHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        throw new NotImplementedException("JP/JPE is not supported (PF flag not tracked)");
    }
}

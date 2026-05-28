using UltraDecompiler.Disassembler;

namespace UltraDecompiler.Decompilation.InstructionHandlers;

public class JcxzHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return new CmpExpr(CmpOperation.Eq, block.EndRegisters.Get16(1), ConstExpr.Zero);
    }
}

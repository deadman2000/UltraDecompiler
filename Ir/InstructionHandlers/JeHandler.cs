using UltraDecompiler.Decompilation;
using UltraDecompiler.Ir.Expressions;

namespace UltraDecompiler.Ir.InstructionHandlers;

public class JeHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return block.EndRegisters.ZF;
    }
}

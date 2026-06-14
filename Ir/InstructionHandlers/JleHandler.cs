using UltraDecompiler.Decompilation;
using UltraDecompiler.Ir.Expressions;
using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.InstructionHandlers;

public class JleHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return block.EndRegisters.ZF | block.EndRegisters.SfNeOf();
    }
}

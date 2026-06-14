using UltraDecompiler.Decompilation;
using UltraDecompiler.Ir.Expressions;

namespace UltraDecompiler.Ir.InstructionHandlers;

public class JcxzHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return new CmpExpr(CmpOperation.Eq, block.EndRegisters.Get16(GpRegister16.CX), ConstExpr.Zero);
    }
}

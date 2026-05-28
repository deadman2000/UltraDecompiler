using UltraDecompiler.Disassembler;

namespace UltraDecompiler.Decompilation.InstructionHandlers;

public class JaHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return !block.EndRegisters.CF & !block.EndRegisters.ZF;
    }
}

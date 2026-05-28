using UltraDecompiler.Disassembler;

namespace UltraDecompiler.Decompilation.InstructionHandlers;

public class JsHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return block.EndRegisters.SF;
    }
}

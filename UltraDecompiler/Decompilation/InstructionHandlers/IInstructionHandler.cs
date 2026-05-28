using UltraDecompiler.Disassembler;

namespace UltraDecompiler.Decompilation.InstructionHandlers;

public interface IInstructionHandler
{
    void Handle(ExprBlock block, Instruction instr);
}

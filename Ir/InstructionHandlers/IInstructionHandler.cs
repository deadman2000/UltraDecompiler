namespace UltraDecompiler.Ir.InstructionHandlers;

public interface IInstructionHandler
{
    void Handle(ExprBlock block, Instruction instr);
}

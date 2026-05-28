using UltraDecompiler.Disassembler;

namespace UltraDecompiler.Decompilation.InstructionHandlers;

public class LodsHandler : BaseStringHandler
{
    public override void Handle(ExprBlock block, Instruction instr)
    {
        int size = GetOperationSize(instr.Mnemonic);

        if (instr.HasRepPrefix)
        {
            EmitRepStringLoop(block, instr, size, StringOpKind.Load);
            return;
        }

        Expr value = BuildStringMemoryRead(block, instr, isSource: true, size);

        if (size == 1)
            block.EndRegisters = block.EndRegisters.Set8(0, value);
        else
            block.EndRegisters = block.EndRegisters.Set16(0, value);

        UpdateStringPointers(block, size, updateSi: true, updateDi: false);
    }
}

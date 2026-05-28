using UltraDecompiler.Disassembler;

namespace UltraDecompiler.Decompilation.InstructionHandlers;

public class StosHandler : BaseStringHandler
{
    public override void Handle(ExprBlock block, Instruction instr)
    {
        int size = GetOperationSize(instr.Mnemonic);

        if (instr.HasRepPrefix)
        {
            EmitRepStringLoop(block, instr, size, StringOpKind.Store);
            return;
        }

        Expr value = size == 1
            ? block.EndRegisters.Get8(0)
            : block.EndRegisters.Get16(0);

        var (dstAddr, dstSeg) = BuildStringMemoryAddress(block, isDestination: true);

        block.Operations.Add(new StoreOperation(dstAddr, dstSeg, value));
        UpdateStringPointers(block, size, updateSi: false, updateDi: true);
    }
}

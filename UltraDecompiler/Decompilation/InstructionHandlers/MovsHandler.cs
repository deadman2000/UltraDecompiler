using UltraDecompiler.Disassembler;

namespace UltraDecompiler.Decompilation.InstructionHandlers;

public class MovsHandler : BaseStringHandler
{
    public override void Handle(ExprBlock block, Instruction instr)
    {
        int size = GetOperationSize(instr.Mnemonic);

        if (instr.HasRepPrefix)
        {
            // REP пока работает через старую логику
            EmitRepStringLoop(block, instr, size, StringOpKind.Move);
            return;
        }

        // Не-REP версия
        Expr src = BuildStringMemoryRead(block, instr, isSource: true, size);
        var (dstAddr, dstSeg) = BuildStringMemoryAddress(block, isDestination: true);

        block.Operations.Add(new StoreOperation(dstAddr, dstSeg, src));
        UpdateStringPointers(block, size, updateSi: true, updateDi: true);
    }
}

using UltraDecompiler.Disassembler;

namespace UltraDecompiler.Decompilation.InstructionHandlers;

public class ScasHandler : BaseStringHandler
{
    public override void Handle(ExprBlock block, Instruction instr)
    {
        int size = GetOperationSize(instr.Mnemonic);

        if (instr.HasRepPrefix)
        {
            EmitRepCompareScanLoop(block, instr, size, isCompare: false);
            return;
        }

        Expr left = size == 1
            ? block.EndRegisters.Get8(0)
            : block.EndRegisters.Get16(0);

        Expr right = BuildStringMemoryRead(block, instr, isSource: false, size);

        block.EndRegisters = block.EndRegisters with
        {
            ZF = new CmpExpr(CmpOperation.Eq, left, right),
            CF = new CmpExpr(CmpOperation.Ult, left, right)
        };

        UpdateStringPointers(block, size, updateSi: false, updateDi: true);
    }
}

namespace UltraDecompiler.Decompilation.InstructionHandlers;

public class CmpsHandler : BaseStringHandler
{
    public override void Handle(ExprBlock block, Instruction instr)
    {
        int size = GetOperationSize(instr.Mnemonic);

        if (instr.HasRepPrefix)
        {
            EmitRepCompareScanLoop(block, instr, size, isCompare: true);
            return;
        }

        Expr left = BuildStringMemoryRead(block, instr, isSource: true, size);
        Expr right = BuildStringMemoryRead(block, instr, isSource: false, size);

        block.EndRegisters = block.EndRegisters with
        {
            ZF = new CmpExpr(CmpOperation.Eq, left, right),
            CF = new CmpExpr(CmpOperation.Ult, left, right)
        };

        UpdateStringPointers(block, size, updateSi: true, updateDi: true);
    }
}

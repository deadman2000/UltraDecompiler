using UltraDecompiler.Decompilation;
using UltraDecompiler.Disassembler;
using UltraDecompiler.PostProcessing;

namespace DecompilerTests.PostProcessing;

public class StackLocalArrayInferrerTests
{
    [Fact]
    public void Infer_TwoStackArrays_AssignsSizesByOffsetBoundaries()
    {
        var expressions = new ExpressionBuilder();
        expressions.Variables.ActivateStackLocals([-50, -20]);

        var stackLocals = expressions.Variables.StackLocals;
        Assert.Equal(2, stackLocals.Count);

        var procedure = new DisassembledProcedure
        {
            Offset = 0x48,
            Instructions = [LeaLocal(-50), LeaLocal(-20)],
            Expressions = expressions,
            Name = "main",
        };

        StackLocalArrayInferrer.Infer(procedure, []);

        Assert.Equal(-50, stackLocals[0].Offset);
        Assert.Equal(-20, stackLocals[1].Offset);
        Assert.Equal(30, stackLocals[0].Variable.ArraySize);
        Assert.Equal(20, stackLocals[1].Variable.ArraySize);
        Assert.Equal(CType.Char, stackLocals[0].Variable.Type);
        Assert.Equal(CType.Char, stackLocals[1].Variable.Type);
    }

    [Fact]
    public void Infer_ScalarLocalsBetweenArrays_StayInt()
    {
        var expressions = new ExpressionBuilder();
        expressions.Variables.ActivateStackLocals([-54, -52, -22, -20]);

        var stackLocals = expressions.Variables.StackLocals;
        var procedure = new DisassembledProcedure
        {
            Offset = 0x48,
            Instructions = [LeaLocal(-52), LeaLocal(-20)],
            Expressions = expressions,
            Name = "main",
        };

        StackLocalArrayInferrer.Infer(procedure, []);

        Assert.Null(stackLocals[0].Variable.ArraySize);
        Assert.Equal(30, stackLocals[1].Variable.ArraySize);
        Assert.Null(stackLocals[2].Variable.ArraySize);
        Assert.Equal(20, stackLocals[3].Variable.ArraySize);
    }

    [Fact]
    public void Infer_SingleStackArray_UsesDistanceToBp()
    {
        var expressions = new ExpressionBuilder();
        expressions.Variables.ActivateStackLocals([-20]);

        var buf = expressions.Variables.StackLocals[0].Variable;
        var procedure = new DisassembledProcedure
        {
            Offset = 0x48,
            Instructions = [LeaLocal(-20)],
            Expressions = expressions,
            Name = "main",
        };

        StackLocalArrayInferrer.Infer(procedure, []);

        Assert.Equal(20, buf.ArraySize);
        Assert.Equal(CType.Char, buf.Type);
    }

    private static Instruction LeaLocal(int bpDisplacement) =>
        new()
        {
            Mnemonic = Mnemonic.LEA,
            Operand1 = Operand.AX,
            Operand2 = new Operand(OperandType.Memory, bpDisplacement, AddressRegister.BP),
        };
}

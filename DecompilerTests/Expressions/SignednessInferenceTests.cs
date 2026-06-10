using UltraDecompiler.Decompilation;
using UltraDecompiler.Decompilation.Operations;
using UltraDecompiler.Disassembler;
using UltraDecompiler.Headers;
using UltraDecompiler.PostProcessing;

namespace DecompilerTests.Expressions;

public class SignednessInferenceTests : BaseTests
{
    [Fact]
    public void Cwd_MarksAxVariableAsSignedInt()
    {
        var expr = BuildExpressions("99", vars =>
        {
            var v = vars.CreateVariable("val");
            return RegisterExpressions.InitZero().Set16(GpRegister16.AX, v);
        });

        var ax = Assert.IsType<Variable>(expr.Blocks[0].EndRegisters.AX);
        Assert.Equal(CTypeKind.Int, ax.Type?.Kind);
    }

    [Fact]
    public void Cbw_MarksAlVariableAsChar()
    {
        Variable? chVar = null;
        BuildExpressions("98", vars =>
        {
            chVar = vars.CreateVariable("ch");
            return RegisterExpressions.InitZero().Set8(GpRegister8.AL, chVar);
        });

        Assert.Equal(CTypeKind.Char, chVar!.Type?.Kind);
    }

    [Fact]
    public void Sar_MarksDestinationAsSigned()
    {
        var expr = BuildExpressions("D1 F8", vars =>
        {
            var v = vars.CreateVariable("x");
            return RegisterExpressions.InitZero().Set16(GpRegister16.AX, v);
        });

        var ax = Assert.IsType<Variable>(expr.Blocks[0].EndRegisters.AX);
        Assert.Equal(CTypeKind.Int, ax.Type?.Kind);
    }

    [Fact]
    public void Shr_MarksDestinationAsUnsigned()
    {
        var expr = BuildExpressions("D1 E8", vars =>
        {
            var v = vars.CreateVariable("x");
            return RegisterExpressions.InitZero().Set16(GpRegister16.AX, v);
        });

        var ax = Assert.IsType<Variable>(expr.Blocks[0].EndRegisters.AX);
        Assert.Equal(CTypeKind.Unsigned, ax.Type?.Kind);
    }

    [Fact]
    public void Imul_MarksOperandsAsSigned()
    {
        var expr = BuildExpressions("""
            F7 E9       ; imul cx
            """, vars =>
        {
            var ax = vars.CreateVariable("a");
            var cx = vars.CreateVariable("b");
            return RegisterExpressions.InitZero()
                .Set16(GpRegister16.AX, ax)
                .Set16(GpRegister16.CX, cx);
        });

        var block = expr.Blocks[0];
        var ax = Assert.IsType<Variable>(block.EndRegisters.AX);
        Assert.Equal(CTypeKind.Int, ax.Type?.Kind);
    }

    [Fact]
    public void Mul_MarksOperandsAsUnsigned()
    {
        var expr = BuildExpressions("""
            F7 E1       ; mul cx
            """, vars =>
        {
            var ax = vars.CreateVariable("a");
            var cx = vars.CreateVariable("b");
            return RegisterExpressions.InitZero()
                .Set16(GpRegister16.AX, ax)
                .Set16(GpRegister16.CX, cx);
        });

        var block = expr.Blocks[0];
        var ax = Assert.IsType<Variable>(block.EndRegisters.AX);
        Assert.Equal(CTypeKind.Unsigned, ax.Type?.Kind);
    }

    [Fact]
    public void CmpFollowedByJb_MarksComparedVariablesAsUnsigned()
    {
        var graph = GetGraph("""
            3D 05 00    ; cmp ax, 5
            72 00       ; jb $+0
            C3          ; ret
            """);

        var builder = new ExpressionBuilder();
        var left = builder.Variables.CreateVariable("left");
        builder.Build(graph, RegisterExpressions.InitZero().Set16(GpRegister16.AX, left), []);

        Assert.Equal(CTypeKind.Unsigned, left.Type?.Kind);
    }

    [Fact]
    public void CmpFollowedByJl_MarksComparedVariablesAsSigned()
    {
        var graph = GetGraph("""
            3D 05 00    ; cmp ax, 5
            7C 00       ; jl $+0
            C3          ; ret
            """);

        var builder = new ExpressionBuilder();
        var left = builder.Variables.CreateVariable("left");
        builder.Build(graph, RegisterExpressions.InitZero().Set16(GpRegister16.AX, left), []);

        Assert.Equal(CTypeKind.Int, left.Type?.Kind);
    }

    [Fact]
    public void Neg_MarksOperandAsSigned()
    {
        var expr = BuildExpressions("F7 D8", vars =>
        {
            var v = vars.CreateVariable("x");
            return RegisterExpressions.InitZero().Set16(GpRegister16.AX, v);
        });

        var ax = Assert.IsType<Variable>(expr.Blocks[0].EndRegisters.AX);
        Assert.Equal(CTypeKind.Int, ax.Type?.Kind);
    }

    [Fact]
    public void VariableTypeInferrer_PropagatesUnsignedThroughAssignment()
    {
        var src = new Variable(0) { Type = CType.UnsignedInt };
        var dst = new Variable(1);
        var operations = new List<Operation>
        {
            new SetOperation(dst, src),
        };

        VariableTypeInferrer.Infer(operations, new ProcedureStorage(), HeaderCatalog.Load("__nonexistent__"));

        Assert.Equal(CTypeKind.Unsigned, dst.Type?.Kind);
    }
}

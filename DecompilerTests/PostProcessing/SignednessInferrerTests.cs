using UltraDecompiler.Decompilation;
using UltraDecompiler.Decompilation.Operations;
using UltraDecompiler.PostProcessing;

namespace DecompilerTests.PostProcessing;

/// <summary>Вывод <c>unsigned</c> в сигнатуре по типам переменных IR.</summary>
public sealed class SignednessInferrerTests
{
    // return с unsigned-операндом → unsigned return type
    [Fact]
    public void Infer_UnsignedReturnOperand_SetsUnsignedReturnType()
    {
        var arg0 = new Variable(0) { Type = CType.UnsignedInt };
        var procedure = new DisassembledProcedure
        {
            Offset = 0x10,
            Instructions = [],
            Name = "sub_0010",
            Signature = new ProcedureSignature(CType.Int, []),
            Expressions = new ExpressionBuilder(),
        };

        var operations = new List<Operation>
        {
            new ReturnOperation(
                new Math2Expr(Math2Operation.Shl, arg0, new ConstExpr(3)),
                IsExplicit: true),
        };

        SignednessInferrer.Infer(procedure, operations);

        Assert.Equal(CTypeKind.Unsigned, procedure.Signature.ReturnType.Kind);
    }
}

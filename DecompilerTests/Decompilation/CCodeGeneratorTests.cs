using UltraDecompiler.CodeGeneration;
using UltraDecompiler.Decompilation;
using UltraDecompiler.Decompilation.Operations;

namespace DecompilerTests.Decompilation;

public class CCodeGeneratorTests : BaseTests
{
    [Fact]
    public void FormatCFunction_DeclaresStackLocal_ExcludesParameter()
    {
        var expr = BuildExpressions("""
            55          ; push bp
            8B EC       ; mov bp, sp
            83 EC 02    ; sub sp, 2
            8B 46 04    ; mov ax, [bp+4]
            89 46 FE    ; mov [bp-2], ax
            8B 46 FE    ; mov ax, [bp-2]
            C3          ; ret
            """);

        Assert.Single(expr.Parameters);

        var procedure = new DisassembledProcedure
        {
            Offset = 0,
            Instructions = [],
            Expressions = expr,
            Name = "copy_arg",
            IsLibrary = false,
            Signature = new ProcedureSignature(CType.Int, [
                new ProcedureParameter(CType.Int, new StackParameter(4)),
            ]),
        };

        var source = CCodeGenerator.FormatCFunction(procedure, expr.GetAllOperations());

        Assert.Contains("int copy_arg(int arg0)", source);
        Assert.DoesNotContain("    int arg0;", source);
        Assert.Matches(@"    int var\d+;", source);
        Assert.Contains("arg0", source);
    }

    [Fact]
    public void FormatCFunction_DeclaresVariablesUsedInConditions()
    {
        var local = new Variable(3);
        var expr = BuildExpressions("C3");

        var procedure = new DisassembledProcedure
        {
            Offset = 0,
            Instructions = [],
            Expressions = expr,
            Name = "branch",
            IsLibrary = false,
            Signature = ProcedureSignature.Unknown,
        };

        var operations = new List<Operation>
        {
            new IfOperation(
                new CmpExpr(CmpOperation.Eq, local, new ConstExpr(0)),
                [new ReturnOperation(new ConstExpr(1))],
                [new ReturnOperation(new ConstExpr(0))]),
        };

        var source = CCodeGenerator.FormatCFunction(procedure, operations);

        Assert.Contains("    int var3;", source);
        Assert.Contains("if (var3 == 0)", source);
    }

    [Fact]
    public void FormatCFunction_NoLocals_NoDeclarationBlock()
    {
        var expr = BuildExpressions("C3");

        var procedure = new DisassembledProcedure
        {
            Offset = 0,
            Instructions = [],
            Expressions = expr,
            Name = "empty",
            IsLibrary = false,
            Signature = new ProcedureSignature(CType.Void, []),
        };

        var source = CCodeGenerator.FormatCFunction(procedure, []);

        Assert.DoesNotContain("    int ", source);
        Assert.Contains("    ;", source);
    }
}

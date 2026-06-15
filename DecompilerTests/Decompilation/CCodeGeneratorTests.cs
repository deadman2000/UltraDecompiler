using UltraDecompiler.CodeGeneration;

namespace DecompilerTests.Decompilation;

/// <summary>Генерация тела функции: объявление локалей, параметров и пустых тел.</summary>
public class CCodeGeneratorTests : BaseTests
{
    // Пролог copy_arg(int arg0): локаль на [bp-2], параметр не дублируется как int arg0;
    // Ожидаемый фрагмент:
    //   int copy_arg(int arg0) { int var1; ... }
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

        var source = CCodeGenerator.FormatCFunction(procedure.ToCodegenModel(), expr.GetAllOperations());

        Assert.Contains("int copy_arg(int arg0)", source);
        Assert.DoesNotContain("    int arg0;", source);
        Assert.Matches(@"    int var\d+;", source);
        Assert.Contains("arg0", source);
    }

    // Переменная, используемая только в if, всё равно объявляется в прологе
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

        var source = CCodeGenerator.FormatCFunction(procedure.ToCodegenModel(), operations);

        Assert.Contains("    int var3;", source);
        Assert.Contains("if (var3 == 0)", source);
    }

    // void без явного return в QuickC: линейный RET → ReturnOperation не печатается
    [Fact]
    public void FormatCFunction_VoidImplicitReturn_OmitsBareReturn()
    {
        var expr = BuildExpressions("C3");

        var procedure = new DisassembledProcedure
        {
            Offset = 0,
            Instructions = [],
            Expressions = expr,
            Name = "foo",
            IsLibrary = false,
            Signature = new ProcedureSignature(CType.Void, []),
        };

        var source = CCodeGenerator.FormatCFunction(
            procedure.ToCodegenModel(),
            [new ReturnOperation(ConstExpr.Zero)]);

        Assert.DoesNotContain("return", source);
        Assert.Contains("void foo(void)", source);
    }

    // void с явным return в QuickC: JMP на эпилог → голый return в C
    [Fact]
    public void FormatCFunction_VoidExplicitReturn_EmitsBareReturn()
    {
        var expr = BuildExpressions("C3");

        var procedure = new DisassembledProcedure
        {
            Offset = 0,
            Instructions = [],
            Expressions = expr,
            Name = "foo_ret",
            IsLibrary = false,
            Signature = new ProcedureSignature(CType.Void, []),
        };

        var source = CCodeGenerator.FormatCFunction(
            procedure.ToCodegenModel(),
            [new ReturnOperation(ConstExpr.Zero, IsExplicit: true)]);

        Assert.Contains("    return;", source);
    }

    // Пустое тело void-функции — нет блока int varN;, только комментарий-заглушка
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

        var source = CCodeGenerator.FormatCFunction(procedure.ToCodegenModel(), []);

        Assert.DoesNotContain("    int ", source);
        Assert.Contains("    ;", source);
    }
}

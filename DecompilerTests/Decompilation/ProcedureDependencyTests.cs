using TestSupport;
using UltraDecompiler.CodeGeneration;

namespace DecompilerTests.Decompilation;

/// <summary>Граф вызовов, #include и заголовки между процедурами.</summary>
public class ProcedureDependencyTests : BaseTests
{
    // Вызовы внутри if/else — все callees собираются (sub_0010, printf, _disable)
    [Fact]
    public void Collect_FindsCallsInNestedControlFlow()
    {
        var operations = new List<Operation>
        {
            new SetOperation(new Variable(0).ToSet(), new CallExpr("sub_0010", [new ConstExpr(1), new ConstExpr(2)])),
            new IfOperation(
                new CmpExpr(CmpOperation.Eq, new ConstExpr(0), new ConstExpr(1)),
                [new CallOperation("printf", [new StringExpr("x")])],
                [new CallOperation("_disable", [])]),
        };

        var callees = ProcedureDependencyCollector.Collect(operations);

        Assert.Equal(["_disable", "printf", "sub_0010"], callees);
    }

    // main вызывает sub_0010 и printf → #include "sub_0010.h" и <STDIO.H>
    [Fact]
    public void ResolveIncludes_UserProcedureAndStdioHeader()
    {
        var storage = new ProcedureStorage();
        var addProc = new DisassembledProcedure
        {
            Offset = 0x10,
            Instructions = [],
            Expressions = BuildExpressions("C3"),
            Name = "sub_0010",
            IsLibrary = false,
            Signature = new ProcedureSignature(CType.Int, [
                new ProcedureParameter(CType.Int, new StackParameter(4)),
                new ProcedureParameter(CType.Int, new StackParameter(6)),
            ]),
        };
        var mainProc = new DisassembledProcedure
        {
            Offset = 0x20,
            Instructions = [],
            Expressions = BuildExpressions("C3"),
            Name = "main",
            IsLibrary = false,
            Signature = new ProcedureSignature(CType.Int, []),
        };
        storage.Add(addProc);
        storage.Add(mainProc);

        var catalog = HeaderCatalog.Load(QuickCTestAssets.IncludeDirectory);
        var includes = ProcedureIncludeResolver.ResolveIncludes(
            mainProc,
            ["sub_0010", "printf"],
            storage,
            catalog);

        Assert.Contains("<STDIO.H>", includes);
        Assert.Contains("\"sub_0010.h\"", includes);
    }

    // В список .c для генерации попадает sub_0010, но не main (точка входа не «вызывается»)
    [Fact]
    public void CollectReferencedUserProcedureNames_SkipsUncalledEntryPoint()
    {
        var storage = new ProcedureStorage();
        var addProc = new DisassembledProcedure
        {
            Offset = 0x10,
            Instructions = [],
            Expressions = BuildExpressions("C3"),
            Name = "sub_0010",
            IsLibrary = false,
        };
        var mainProc = new DisassembledProcedure
        {
            Offset = 0x20,
            Instructions = [],
            Expressions = BuildExpressions("C3"),
            Name = "main",
            IsLibrary = false,
            Callees = ["sub_0010", "printf"],
        };
        storage.Add(addProc);
        storage.Add(mainProc);

        var referenced = ProcedureDependencyCollector.CollectReferencedUserProcedureNames(
            [mainProc, addProc],
            storage);

        Assert.Contains("sub_0010", referenced);
        Assert.DoesNotContain("main", referenced);
    }

    // sub_0010.h: include guard и прототип int sub_0010(int arg0, int arg1);
    [Fact]
    public void FormatHeaderFile_EmitsPrototype()
    {
        var procedure = new DisassembledProcedure
        {
            Offset = 0x10,
            Instructions = [],
            Expressions = BuildExpressions("C3"),
            Name = "sub_0010",
            IsLibrary = false,
            Signature = new ProcedureSignature(CType.Int, [
                new ProcedureParameter(CType.Int, new StackParameter(4)),
                new ProcedureParameter(CType.Int, new StackParameter(6)),
            ]),
        };

        var header = CCodeGenerator.FormatHeaderFile(procedure.ToCodegenModel());

        Assert.Contains("int sub_0010(int arg0, int arg1);", header);
        Assert.Contains("#ifndef SUB_0010_H", header);
    }
}

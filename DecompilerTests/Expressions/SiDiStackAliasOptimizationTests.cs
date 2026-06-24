using TestSupport;
using UltraDecompiler.Common;

namespace DecompilerTests.Expressions;

/// <summary>Тесты подстановки стековых varN вместо regSI/regDI в /Ox.</summary>
public sealed class SiDiStackAliasOptimizationTests : BaseTests
{
    /// <summary>
    /// /Ox: цикл for_step3 — regSI/regDI заменяются на var2/var1, нет копий в эпилоге.
    /// </summary>
    [Fact]
    public void Loopspec_ForStep3_Ox_NoSiDiRegisters()
    {
        var procedure = GetLoopspecProcedure(0x0010);
        Assert.NotNull(procedure.Expressions);

        var operations = procedure.Expressions!.Blocks.SelectMany(static b => b.Operations).ToList();
        AssertNoSiDiReferences(operations);
        Assert.Contains(operations, op => op is SetOperation { Src: ConstExpr { Value: 0 } });
        Assert.Contains(operations, op => op is ReturnOperation);
    }

    /// <summary>
    /// /Ox: for_multi_var — regSI→var2, regDI→var3, тело цикла без регистров SI/DI.
    /// </summary>
    [Fact]
    public void Loopspec_ForMultiVar_Ox_NoSiDiRegisters()
    {
        var procedure = GetLoopspecProcedure(0x00AA);
        Assert.NotNull(procedure.Expressions);

        var operations = procedure.Expressions!.Blocks.SelectMany(static b => b.Operations).ToList();
        AssertNoSiDiReferences(operations);
    }

    /// <summary>
    /// /Ox: for_break — свопнутые роли sum/i в SI/DI корректно мапятся на var2/var1.
    /// </summary>
    [Fact]
    public void Loopspec_ForBreak_Ox_NoSiDiRegisters()
    {
        var procedure = GetLoopspecProcedure(0x0208);
        Assert.NotNull(procedure.Expressions);

        var operations = procedure.Expressions!.Blocks.SelectMany(static b => b.Operations).ToList();
        AssertNoSiDiReferences(operations);
    }

    /// <summary>
    /// /Ox: for_empty_body — только regSI, подставляется var1.
    /// </summary>
    [Fact]
    public void Loopspec_ForEmptyBody_Ox_NoSiDiRegisters()
    {
        var procedure = GetLoopspecProcedure(0x01AE);
        Assert.NotNull(procedure.Expressions);

        var operations = procedure.Expressions!.Blocks.SelectMany(static b => b.Operations).ToList();
        AssertNoSiDiReferences(operations);
        Assert.Contains(operations, op => op is ReturnOperation { Value: VariableExpr { Var.Name: "var1" } });
    }

    private static DisassembledProcedure GetLoopspecProcedure(int offset)
    {
        var procedures = DecompileTestHelper.GetExampleIR(
            "loopspec.c",
            optimization: OptimizationLevel.EnabledFull);
        return procedures.Single(p => p.Offset == offset);
    }

    private static void AssertNoSiDiReferences(IEnumerable<Operation> operations)
    {
        foreach (var operation in operations)
        {
            Assert.False(ReferencesSiDi(operation), $"regSI/regDI в операции: {operation}");
        }
    }

    private static bool ReferencesSiDi(Operation operation) => operation switch
    {
        SetOperation set => ReferencesSiDi(set.Dst) || ReferencesSiDi(set.Src),
        IncOperation inc => ReferencesSiDi(inc.Target),
        DecOperation dec => ReferencesSiDi(dec.Target),
        AddAssignOperation add => ReferencesSiDi(add.Target) || ReferencesSiDi(add.Value),
        SubAssignOperation sub => ReferencesSiDi(sub.Target) || ReferencesSiDi(sub.Value),
        ReturnOperation { Value: { } value } => ReferencesSiDi(value),
        StoreOperation store => ReferencesSiDi(store.Value) || ReferencesSiDi(store.Address),
        CallOperation call => call.Args.Any(ReferencesSiDi),
        _ => false,
    };

    private static bool ReferencesSiDi(Expr? expr) => expr switch
    {
        null => false,
        VariableExpr { Var.Name: "regSI" or "regDI" } => true,
        Math1Expr math1 => ReferencesSiDi(math1.Op),
        Math2Expr math2 => ReferencesSiDi(math2.First) || ReferencesSiDi(math2.Second),
        CmpExpr cmp => ReferencesSiDi(cmp.Left) || ReferencesSiDi(cmp.Right),
        CallExpr call => call.Args.Any(ReferencesSiDi),
        MemExpr mem => ReferencesSiDi(mem.Address) || ReferencesSiDi(mem.Segment),
        IncDecExpr inc => ReferencesSiDi(inc.Operand),
        AddressOfExpr addr => ReferencesSiDi(addr.Operand),
        MemberExpr member => ReferencesSiDi(member.Base),
        LongExpr longExpr => ReferencesSiDi(longExpr.Low) || ReferencesSiDi(longExpr.High),
        _ => false,
    };
}

namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты IncOperation/DecOperation по ассемблерным INC/DEC и ADD/SUB [mem], 1.
/// </summary>
public class IncDecOperationTests : BaseTests
{
    // inc word [bp-2] → IncOperation на стековой локали
    [Fact]
    public void Inc_StackLocal_EmitsIncOperation()
    {
        var expr = BuildExpressions("""
            55         ; push bp
            8B EC      ; mov bp, sp
            FF 46 FE   ; inc word ptr [bp-2]
            """);

        var block = expr.Blocks[0];
        var inc = Assert.IsType<IncOperation>(block.Operations[^1]);
        Assert.IsType<Variable>(inc.Target);
        Assert.Contains("++", inc.ToCString(asStatement: true));
    }

    // dec word [bp-2] → DecOperation
    [Fact]
    public void Dec_StackLocal_EmitsDecOperation()
    {
        var expr = BuildExpressions("""
            55         ; push bp
            8B EC      ; mov bp, sp
            FF 4E FE   ; dec word ptr [bp-2]
            """);

        var block = expr.Blocks[0];
        var dec = Assert.IsType<DecOperation>(block.Operations[^1]);
        Assert.IsType<Variable>(dec.Target);
        Assert.Contains("--", dec.ToCString(asStatement: true));
    }

    // add [bp-2], 1 нормализуется в inc локали
    [Fact]
    public void Add_MemoryByOne_EmitsIncOperation()
    {
        var expr = BuildExpressions("""
            55         ; push bp
            8B EC      ; mov bp, sp
            83 46 FE 01 ; add word ptr [bp-2], 1
            """);

        var block = expr.Blocks[0];
        Assert.IsType<IncOperation>(block.Operations[^1]);
    }

    [Fact]
    public void Inc_RegisterWithVariable_EmitsIncOperation()
    {
        var expr = BuildExpressions("40", vars =>
        {
            var prev = vars.CreateVariable("x");
            return RegisterExpressions.InitZero().Set16(GpRegister16.AX, prev);
        });

        var block = expr.Blocks[0];
        var inc = Assert.IsType<IncOperation>(Assert.Single(block.Operations));
        Assert.Equal("x", Assert.IsType<Variable>(inc.Target).Name);
    }

    [Fact]
    public void MovAddMov_LocalPlusOne_StaysSetOperation()
    {
        // QuickC для a = a + 1: mov ax, [bp-2]; add ax, 1; mov [bp-2], ax
        var expr = BuildExpressions("""
            55           ; push bp
            8B EC        ; mov bp, sp
            8B 46 FE     ; mov ax, [bp-2]
            83 C0 01     ; add ax, 1
            89 46 FE     ; mov [bp-2], ax
            """);

        var block = expr.Blocks[0];
        Assert.DoesNotContain(block.Operations, static op => op is IncOperation or DecOperation);
        Assert.Contains(block.Operations, static op => op is SetOperation);
    }

    [Fact]
    public void MovAddMov_LocalMinusOne_EmitsSubNotIncDec()
    {
        // QuickC для a = a - 1: mov ax, [bp-2]; add ax, 0FFFFh; mov [bp-2], ax
        var expr = BuildExpressions("""
            55           ; push bp
            8B EC        ; mov bp, sp
            8B 46 FE     ; mov ax, [bp-2]
            05 FF FF     ; add ax, 0FFFFh
            89 46 FE     ; mov [bp-2], ax
            """);

        var block = expr.Blocks[0];
        Assert.DoesNotContain(block.Operations, static op => op is IncOperation or DecOperation);
        var tempSet = Assert.IsType<SetOperation>(block.Operations[^2]);
        var math = Assert.IsType<Math2Expr>(tempSet.Src);
        Assert.Equal(Math2Operation.Sub, math.Operation);
        Assert.IsType<SetOperation>(block.Operations[^1]);
    }

    [Fact]
    public void Sub_MemoryByOne_EmitsDecOperation_NotFromAddFFFF()
    {
        var expr = BuildExpressions("""
            55         ; push bp
            8B EC      ; mov bp, sp
            83 6E FE 01 ; sub word ptr [bp-2], 1
            """);

        Assert.IsType<DecOperation>(expr.Blocks[0].Operations[^1]);
    }
}

using UltraDecompiler.PostProcessing.Normalization;

namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты AddAssignOperation/SubAssignOperation по QuickC add/sub [mem], imm|reg.
/// </summary>
public class CompoundAssignOperationTests : BaseTests
{
    // add word ptr [bp-2], 5 → var1 += 5
    [Fact]
    public void Add_MemoryByConst_EmitsAddAssignOperation()
    {
        var expr = BuildExpressions("""
            55           ; push bp
            8B EC        ; mov bp, sp
            83 46 FE 05  ; add word ptr [bp-2], 5
            """);

        var add = Assert.IsType<AddAssignOperation>(expr.Blocks[0].Operations[^1]);
        Assert.IsType<Variable>(add.Target);
        Assert.Contains("+=", add.ToCString(asStatement: true));
    }

    // sub word ptr [bp-2], 5 → var1 -= 5
    [Fact]
    public void Sub_MemoryByConst_EmitsSubAssignOperation()
    {
        var expr = BuildExpressions("""
            55           ; push bp
            8B EC        ; mov bp, sp
            83 6E FE 05  ; sub word ptr [bp-2], 5
            """);

        Assert.IsType<SubAssignOperation>(expr.Blocks[0].Operations[^1]);
    }

    // mov ax,[bp-4]; add [bp-2],ax → var1 += var2
    [Fact]
    public void Add_MemoryByRegister_EmitsAddAssignOperation()
    {
        var expr = BuildProcExpressions("""
            55           ; push bp
            8B EC        ; mov bp, sp
            81 EC 04 00  ; sub sp, 4
            8B 46 FC     ; mov ax, [bp-4]
            01 46 FE     ; add [bp-2], ax
            """);

        var add = Assert.IsType<AddAssignOperation>(Assert.Single(expr.Blocks[0].Operations));
        Assert.Contains("+=", add.ToCString(asStatement: true));
    }

    // mov/add/mov с константой остаётся var = var + K, не +=
    [Fact]
    public void MovAddMov_Const_StaysExplicitAssign()
    {
        var expr = BuildExpressions("""
            55           ; push bp
            8B EC        ; mov bp, sp
            8B 46 FE     ; mov ax, [bp-2]
            05 05 00     ; add ax, 5
            89 46 FE     ; mov [bp-2], ax
            """);

        var ops = OperationOptimizer.Optimize(expr.Blocks[0].Operations);
        Assert.DoesNotContain(ops, static op => op is AddAssignOperation or SubAssignOperation);
        var set = Assert.IsType<SetOperation>(Assert.Single(ops));
        Assert.Contains("var1 + 5", set.ToCString(asStatement: true));
    }

    // mov/add/mov с переменной → var1 = var1 + var2
    [Fact]
    public void MovAddMov_Variable_FoldsToExplicitAssign()
    {
        var expr = BuildProcExpressions("""
            55           ; push bp
            8B EC        ; mov bp, sp
            81 EC 04 00  ; sub sp, 4
            8B 46 FE     ; mov ax, [bp-2]
            03 46 FC     ; add ax, [bp-4]
            89 46 FE     ; mov [bp-2], ax
            """);

        var ops = OperationOptimizer.Optimize(expr.Blocks[0].Operations);
        Assert.DoesNotContain(ops, static op => op is AddAssignOperation);
        var set = Assert.IsType<SetOperation>(Assert.Single(ops));
        Assert.Contains("var1 + var2", set.ToCString(asStatement: true));
    }
}
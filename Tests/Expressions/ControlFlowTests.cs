using UltraDecompiler.Decompilation;

namespace Tests.Expressions;

/// <summary>
/// Тесты ExpressionBuilder для управления потоком и построения условий на прыжках.
/// </summary>
public class ControlFlowTests : BaseTests
{
    [Fact]
    public void DecompileMultipleBlocksWithJmp()
    {
        var expr = BuildExpressions("""
            B8 01 00 ; mov ax, 1
            EB 00    ; jmp short +0
            90       ; nop (в следующем блоке)
            """);

        Assert.Equal(2, expr.Blocks.Count);
        var block0 = expr.Blocks[0];
        var block1 = expr.Blocks[1];

        Assert.NotNull(block0.Next);
        Assert.Equal(block1, block0.Next);
        Assert.Null(block0.ConditionalBlock);
    }

    // === Тесты BuildJumpCondition ===

    [Fact]
    public void ConditionalJump_CmpJe_ProducesEqualityCondition()
    {
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            3D 05 00 ; cmp ax, 5
            74 01    ; je +1
            90       ; nop (fallthrough)
            90       ; nop (target)
            """);

        var condBlock = expr.Blocks.FirstOrDefault(b => b.ConditionalBlock != null);
        Assert.NotNull(condBlock);
        Assert.NotNull(condBlock.Condition);
        Assert.NotEqual(ConstExpr.One, condBlock.Condition);

        var condition = Assert.IsType<CmpExpr>(condBlock.Condition);
        Assert.Equal(CmpOperation.Eq, condition.Operation);
    }

    [Fact]
    public void ConditionalJump_CmpJne_ProducesNegatedCondition()
    {
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            3D 06 00 ; cmp ax, 6
            75 01    ; jne +1
            90       ; nop (fallthrough)
            90       ; nop (target)
            """);

        var condBlock = expr.Blocks.FirstOrDefault(b => b.ConditionalBlock != null);
        Assert.NotNull(condBlock);

        // Благодаря BoolNot(Eq) теперь сразу получаем Ne (более чистое представление)
        var cond = Assert.IsType<CmpExpr>(condBlock.Condition);
        Assert.Equal(CmpOperation.Ne, cond.Operation);
    }

    [Fact]
    public void ConditionalJump_Arithmetic_Jz_UsesResultVariable()
    {
        var expr = BuildExpressions("""
            B8 01 00 ; mov ax, 1
            05 FF FF ; add ax, -1
            74 01    ; jz +1
            90       ; nop fall
            90       ; nop target
            """);

        var condBlock = expr.Blocks.FirstOrDefault(b => b.ConditionalBlock != null);
        Assert.NotNull(condBlock);
        Assert.NotEqual(ConstExpr.One, condBlock.Condition);

        var cmp = Assert.IsType<CmpExpr>(condBlock.Condition);
        Assert.Equal(CmpOperation.Eq, cmp.Operation);
        var zero = Assert.IsType<ConstExpr>(cmp.Right);
        Assert.Equal(0, zero.Value);
    }

    [Fact]
    public void ConditionalJump_CmpJa_ProducesCompoundCondition()
    {
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            3D 03 00 ; cmp ax, 3
            77 01    ; ja +1
            90       ; nop fall
            90       ; nop target
            """);

        var condBlock = expr.Blocks.FirstOrDefault(b => b.ConditionalBlock != null);
        Assert.NotNull(condBlock);
        Assert.NotEqual(ConstExpr.One, condBlock.Condition);

        // JA = !CF && !ZF → составное выражение (Math2 And)
        var cond = Assert.IsType<Math2Expr>(condBlock.Condition);
        Assert.Equal(Math2Operation.And, cond.Operation);
    }

    [Fact]
    public void ConditionalJump_NoLongerUsesConstOnePlaceholder()
    {
        var expr = BuildExpressions("""
            B8 10 00 ; mov ax, 10h
            3D 05 00 ; cmp ax, 5
            75 01    ; jne +1
            90       ; fall
            90       ; target
            """);

        foreach (var block in expr.Blocks)
        {
            if (block.ConditionalBlock != null)
            {
                Assert.NotEqual(ConstExpr.One, block.Condition);
            }
        }
    }
}

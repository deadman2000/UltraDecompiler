namespace DecompilerTests.Expressions;

/// <summary>
/// <see cref="ExpressionBuilder.RemoveEmptyBlocks"/>: удаление пустых блоков без разрыва CFG.
/// </summary>
public sealed class RemoveEmptyBlocksTests : BaseTests
{
    // JZ / MOV / JMP сходятся в NOP-блок без операций перед RET — NOP убирается, граф остаётся связным.
    [Fact]
    public void RemoveEmptyBlocks_SplicesMergeNopBlock()
    {
        var builder = BuildExpressionsRaw("""
            74 03       ; JZ +3
            B8 02 00    ; MOV AX, 2
            EB 00       ; JMP +0
            90          ; NOP (пустой блок)
            C3          ; RET
            """);

        var countBefore = builder.Blocks.Count;
        Assert.Contains(builder.Blocks, block => IsRemovableEmpty(block));

        builder.RemoveEmptyBlocks();

        Assert.Equal(countBefore - 1, builder.Blocks.Count);
        Assert.DoesNotContain(builder.Blocks, block => IsRemovableEmpty(block));

        var retBlock = Assert.Single(builder.Blocks, block => block.Operations.Any(op => op is ReturnOperation));
        Assert.Contains(builder.Blocks, block => ReferenceEquals(block.Next, retBlock));
    }

    // Несколько подряд пустых блоков (NOP / jmp без IR) схлопываются до MOV → RET.
    [Fact]
    public void RemoveEmptyBlocks_SplicesEmptyBlockChain()
    {
        var builder = BuildExpressionsRaw("""
            B8 01 00    ; MOV AX, 1
            EB 00       ; JMP +0 → NOP
            90          ; NOP
            EB 00       ; JMP +0 → NOP
            90          ; NOP
            C3          ; RET
            """);

        var countBefore = builder.Blocks.Count;
        var emptyBefore = builder.Blocks.Count(IsRemovableEmpty);
        Assert.NotEqual(0, emptyBefore);

        builder.RemoveEmptyBlocks();

        Assert.Equal(countBefore - emptyBefore, builder.Blocks.Count);
        Assert.DoesNotContain(builder.Blocks, IsRemovableEmpty);

        var entry = builder.Blocks.MinBy(block => block.BasicBlock.StartOffset)!;
        var retBlock = builder.Blocks.First(block => block.Operations.Any(op => op is ReturnOperation));
        Assert.Same(retBlock, FollowNextChain(entry));
    }

    private static ExprBlock? FollowNextChain(ExprBlock start)
    {
        var block = start;
        var visited = new HashSet<ExprBlock>();

        while (block is not null && visited.Add(block))
        {
            if (block.Operations.Any(op => op is ReturnOperation))
            {
                return block;
            }

            block = block.Next;
        }

        return null;
    }

    // Блок с исходящим условным переходом не удаляется.
    [Fact]
    public void RemoveEmptyBlocks_KeepsBranchingBlock()
    {
        var builder = BuildExpressionsRaw("""
            74 03       ; JZ +3
            B8 02 00    ; MOV AX, 2
            EB 00       ; JMP +0
            90          ; NOP
            C3          ; RET
            """);

        Assert.Equal(1, builder.Blocks.Count(block => block.Condition is not null));

        builder.RemoveEmptyBlocks();

        Assert.Equal(1, builder.Blocks.Count(block => block.Condition is not null));
    }

    // Optimize() вызывает RemoveEmptyBlocks — пустой merge-блок не попадает в итоговый IR.
    [Fact]
    public void Optimize_RemovesEmptyMergeBlock()
    {
        var builder = BuildExpressions("""
            74 03       ; JZ +3
            B8 02 00    ; MOV AX, 2
            EB 00       ; JMP +0
            90          ; NOP
            C3          ; RET
            """);

        Assert.DoesNotContain(builder.Blocks, block => IsRemovableEmpty(block));
    }

    private static bool IsRemovableEmpty(ExprBlock block) =>
        block.Operations.Count == 0
        && block.ConditionalBlock is null
        && block.Condition is null;
}

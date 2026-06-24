namespace UltraDecompiler.Ir.Builder;

public partial class ExpressionBuilder
{
    /// <summary>
    /// Удаляет пустые блоки IR: без операций и без исходящего условного перехода.
    /// Предшественники связываются напрямую с <see cref="ExprBlock.Next"/> удаляемого блока.
    /// </summary>
    public void RemoveEmptyBlocks()
    {
        if (Blocks.Count == 0)
        {
            return;
        }

        while (RemoveEmptyBlocksOnce())
        {
        }
    }

    private bool RemoveEmptyBlocksOnce()
    {
        var removed = false;

        foreach (var block in Blocks.ToList())
        {
            if (!IsRemovableEmptyBlock(block))
            {
                continue;
            }

            if (block.Next is not null)
            {
                RedirectPredecessors(block, block.Next);
                RemoveBlock(block);
                removed = true;
                continue;
            }

            if (!HasPredecessor(block) && !ReferenceEquals(_entryBlock, block))
            {
                RemoveBlock(block);
                removed = true;
            }
        }

        return removed;
    }

    private bool HasPredecessor(ExprBlock block)
    {
        foreach (var pred in Blocks)
        {
            if (ReferenceEquals(pred.Next, block) || ReferenceEquals(pred.ConditionalBlock, block))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRemovableEmptyBlock(ExprBlock block) =>
        block.Operations.Count == 0
        && block.ConditionalBlock is null
        && block.Condition is null;

    private void RedirectPredecessors(ExprBlock removed, ExprBlock successor)
    {
        foreach (var pred in Blocks)
        {
            if (ReferenceEquals(pred.Next, removed))
            {
                pred.Next = successor;
            }

            if (ReferenceEquals(pred.ConditionalBlock, removed))
            {
                pred.ConditionalBlock = successor;
            }
        }

        if (ReferenceEquals(_entryBlock, removed))
        {
            _entryBlock = successor;
        }
    }

    private void RemoveBlock(ExprBlock block)
    {
        Blocks.Remove(block);
        _blocksMap.Remove(block.BasicBlock);
    }
}

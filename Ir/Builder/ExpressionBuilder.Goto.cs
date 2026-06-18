namespace UltraDecompiler.Ir.Builder;

public partial class ExpressionBuilder
{
    private readonly HashSet<int> _gotoLabelOffsets = [];

    /// <summary>Строит множество смещений, для которых нужны метки (цели безусловных jmp).</summary>
    private void BuildGotoLabelMap()
    {
        _gotoLabelOffsets.Clear();

        var indegree = new Dictionary<int, int>();
        foreach (var block in Blocks)
        {
            if (block.Next is not null)
            {
                var offset = block.Next.BasicBlock.StartOffset;
                indegree[offset] = indegree.GetValueOrDefault(offset) + 1;
            }

            if (block.ConditionalBlock is not null)
            {
                var offset = block.ConditionalBlock.BasicBlock.StartOffset;
                indegree[offset] = indegree.GetValueOrDefault(offset) + 1;
            }
        }

        foreach (var block in Blocks)
        {
            if (!EndsWithUnconditionalJump(block) || block.Next is null)
            {
                continue;
            }

            var targetOffset = block.Next.BasicBlock.StartOffset;

            if (block.Operations.Count > 0
                || indegree.GetValueOrDefault(targetOffset) >= 2)
            {
                _gotoLabelOffsets.Add(targetOffset);
            }
        }
    }

    private static string GetLabelForBlock(ExprBlock block) =>
        $"label_{block.BasicBlock.StartOffset:X4}";

    private void TryEmitLabel(ExprBlock block, List<Operation> result)
    {
        if (_gotoLabelOffsets.Contains(block.BasicBlock.StartOffset))
        {
            result.Add(new LabelOperation(GetLabelForBlock(block)));
        }
    }

    private static bool EndsWithUnconditionalJump(ExprBlock block)
    {
        var instructions = block.BasicBlock.Instructions;
        return instructions.Count > 0 && instructions[^1].IsUnconditionalJump;
    }

    /// <summary>
    /// Эмитит <c>goto</c> в конце блока с операциями (типичный <c>goto label;</c> QuickC /Od).
    /// </summary>
    private bool TryEmitStandaloneGoto(
        ExprBlock block,
        List<Operation> result,
        ExprBlock? enclosingLoopExit,
        ExprBlock? enclosingLoopHeader,
        out ExprBlock? target)
    {
        target = null;

        if (!EndsWithUnconditionalJump(block) || block.Next is null)
        {
            return false;
        }

        if (block.Operations.Count == 0)
        {
            return false;
        }

        if (ReferenceEquals(block.Next, enclosingLoopHeader))
        {
            return false;
        }

        if (enclosingLoopExit is not null && ReferenceEquals(block.Next, enclosingLoopExit))
        {
            return false;
        }

        result.Add(new GotoOperation(GetLabelForBlock(block.Next)));
        target = block.Next;
        return true;
    }

    /// <summary>Ветка if состоит только из jmp на merge — это <c>goto</c>, не пустое тело.</summary>
    private static bool IsGotoOnlyBranch(ExprBlock? start, ExprBlock target)
    {
        if (!GotoTargetReaches(start, target))
        {
            return false;
        }

        var block = start;
        for (var step = 0; step < 4 && block is not null && !ReferenceEquals(block, target); step++)
        {
            if (block.Operations.Count > 0)
            {
                return false;
            }

            block = block.Next;
        }

        return true;
    }

    private static bool GotoTargetReaches(ExprBlock? start, ExprBlock target)
    {
        if (start is null)
        {
            return false;
        }

        var visited = new HashSet<ExprBlock>();
        var block = start;

        for (var step = 0; step < 4 && block is not null; step++)
        {
            if (ReferenceEquals(block, target))
            {
                return true;
            }

            if (!visited.Add(block))
            {
                return false;
            }

            if (block.Operations.Count == 0 && ReferenceEquals(block.Next, target))
            {
                return true;
            }

            var instructions = block.BasicBlock.Instructions;
            if (instructions.Count == 1
                && instructions[0].IsUnconditionalJump
                && ReferenceEquals(block.Next, target))
            {
                return true;
            }

            block = block.Next ?? block.ConditionalBlock;
        }

        return false;
    }

    private static void MarkGotoBranchVisited(ExprBlock? start, ExprBlock stop, HashSet<ExprBlock> visited)
    {
        var block = start;

        for (var step = 0; step < 4 && block is not null; step++)
        {
            if (ReferenceEquals(block, stop))
            {
                return;
            }

            visited.Add(block);
            block = block.Next;
        }
    }
}
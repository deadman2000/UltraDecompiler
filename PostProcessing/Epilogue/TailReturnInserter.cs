using UltraDecompiler.Disassembly.Graph;

namespace UltraDecompiler.PostProcessing.Epilogue;

/// <summary>
/// Заменяет переход JMP на общий эпилог явным <see cref="ReturnOperation"/> с текущим AX.
/// </summary>
public static class TailReturnInserter
{
    /// <summary>
    /// Для блоков, завершающихся безусловным JMP в эпилог (pop/leave/ret), добавляет return.
    /// </summary>
    public static void Apply(IReadOnlyList<ExprBlock> blocks, IReadOnlyList<BasicBlock> basicBlocks)
    {
        var blockByOffset = basicBlocks.ToDictionary(static b => b.StartOffset);
        var predecessors = BuildPredecessors(basicBlocks);

        foreach (var block in blocks)
        {
            var instructions = block.BasicBlock.Instructions;
            if (instructions.Count == 0)
            {
                continue;
            }

            var lastInstr = instructions[^1];
            if (!lastInstr.IsUnconditionalJump)
            {
                continue;
            }

            var targetOffset = lastInstr.JumpTarget;
            if (targetOffset < 0 || !blockByOffset.TryGetValue(targetOffset, out var targetBlock))
            {
                continue;
            }

            if (!EpilogueAnalyzer.IsEpilogueTailBlock(targetBlock.Instructions))
            {
                continue;
            }

            var isExplicit = !IsNaturalEpilogueMerge(block.BasicBlock, targetOffset, predecessors);
            var returnValue = block.EndRegisters.Get16(GpRegister16.AX);
            block.Operations.Add(new ReturnOperation(returnValue, IsExplicit: isExplicit));
        }
    }

    /// <summary>
    /// JMP в эпилог сразу после ветки, где jcc тоже ведёт в тот же эпилог — неявный выход (пустой if).
    /// </summary>
    private static bool IsNaturalEpilogueMerge(
        BasicBlock jmpBlock,
        int epilogueOffset,
        IReadOnlyDictionary<BasicBlock, List<BasicBlock>> predecessors)
    {
        if (!predecessors.TryGetValue(jmpBlock, out var preds))
        {
            return false;
        }

        foreach (var pred in preds)
        {
            if (pred.Instructions.Count == 0)
            {
                continue;
            }

            var lastInstr = pred.Instructions[^1];
            if (lastInstr.IsConditionalJump && lastInstr.JumpTarget == epilogueOffset)
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<BasicBlock, List<BasicBlock>> BuildPredecessors(IReadOnlyList<BasicBlock> blocks)
    {
        var predecessors = blocks.ToDictionary(static b => b, static _ => new List<BasicBlock>());

        foreach (var block in blocks)
        {
            if (block.NextBlock is { } next)
            {
                predecessors[next].Add(block);
            }

            if (block.ConditionalBlock is { } conditional)
            {
                predecessors[conditional].Add(block);
            }
        }

        return predecessors;
    }
}

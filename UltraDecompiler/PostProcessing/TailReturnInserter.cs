using UltraDecompiler.Decompilation;
using UltraDecompiler.Decompilation.Operations;
using UltraDecompiler.Graph;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Заменяет переход JMP на общий эпилог явным <see cref="ReturnOperation"/> с текущим AX.
/// </summary>
public static class TailReturnInserter
{
    /// <summary>
    /// Для блоков, завершающихся безусловным JMP в эпилог (pop/leave/ret), добавляет return.
    /// </summary>
    public static void Apply(IReadOnlyList<ExprBlock> blocks, IReadOnlyList<BasicBlock> basicBlocks, byte[] image)
    {
        var blockByOffset = basicBlocks.ToDictionary(static b => b.StartOffset);

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

            var targetOffset = lastInstr.GetEffectiveJumpTarget(image);
            if (targetOffset < 0 || !blockByOffset.TryGetValue(targetOffset, out var targetBlock))
            {
                continue;
            }

            if (!EpilogueAnalyzer.IsEpilogueTailBlock(targetBlock.Instructions))
            {
                continue;
            }

            var returnValue = block.EndRegisters.Get16(GpRegister16.AX);
            block.Operations.Add(new ReturnOperation(returnValue));
        }
    }
}

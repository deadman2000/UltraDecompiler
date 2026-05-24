using UltraDecompiler.Disassembler;

namespace UltraDecompiler.Graph;

/// <summary>
/// Граф потока управления (Control Flow Graph)
/// </summary>
public class ControlFlowGraph
{
    private Dictionary<int, BasicBlock>? _blockByOffset = null;

    public List<BasicBlock> Blocks { get; private set; } = [];

    public BasicBlock EntryBlock { get; private set; } = null!;

    /// <summary>
    /// Строит CFG из списка инструкций
    /// </summary>
    public void Build(X86Disassembler disassembler, int startOffset)
    {
        Queue<int> queue = [];
        queue.Enqueue(startOffset);

        HashSet<int> visited = [];

        while (queue.Count > 0)
        {
            int offset = queue.Dequeue();
            var block = new BasicBlock
            {
                StartOffset = offset
            };
            Blocks.Add(block);
            EntryBlock ??= block;

            foreach (var instr in disassembler.DisassembleBranch(offset))
            {
                block.Instructions.Add(instr);
                visited.Add(instr.Offset);

                if (instr.IsCall)
                {
                    // TODO Нужна проверка, что этот в этом call есть return, т.к. там может быть выход через int 21 AH=4c
                    // Как вариант рекурсивный анализ
                }
            }

            var lastInstr = block.Instructions[^1];
            block.EndOffset = lastInstr.Offset;

            if (lastInstr.IsConditionalJump || lastInstr.IsUnconditionalJump)
            {
                var jumpAddr = lastInstr.GetEffectiveJumpTarget(disassembler.Image);

                // Проверка, что адрес еще не был обработан
                if (!visited.Contains(jumpAddr))
                    queue.Enqueue(jumpAddr);

                if (lastInstr.IsConditionalJump)
                {
                    block.NextOffset = lastInstr.Offset + lastInstr.Size;
                    block.ConditionalOffset = jumpAddr;
                }
                else
                {
                    block.NextOffset = jumpAddr;
                }
            }
        }

        BuildEdges();
    }

    /// <summary>
    /// Сохраняет граф в формате DOT
    /// </summary>
    /// <param name="fileName"></param>
    public void SaveDot(string fileName)
    {
        // TODO
    }

    /// <summary>
    /// Устанавливает взаимосвязи между блоками.
    /// </summary>
    private void BuildEdges()
    {
        _blockByOffset = Blocks.ToDictionary(b => b.StartOffset);

        // Число блоков может меняться, поэтому используем for
        for (int i = 0; i < Blocks.Count; i++)
        {
            var block = Blocks[i];

            if (block.NextOffset.HasValue)
            {
                block.NextBlock = GetBlock(block.NextOffset.Value);
                block.NextOffset = null;
            }

            if (block.ConditionalOffset.HasValue)
            {
                block.ConditionalBlock = GetBlock(block.ConditionalOffset.Value);
                block.ConditionalOffset = null;
            }
        }

        _blockByOffset = null;
    }

    private BasicBlock? GetBlock(int offset)
    {
        // Если пересылаем в начало блока, то возвращаем его
        // Если в середину - разбиваем блок на два

        if (_blockByOffset!.TryGetValue(offset, out var block))
            return block;

        // Ищем блок по диапазонам адресов
        var firstBlock = Blocks.FirstOrDefault(b => b.StartOffset < offset && b.EndOffset >= offset);
        if (firstBlock == null)
        {
            Console.WriteLine($"Block not found {offset:X4}!!!");
            return null;
        }

        var nextBlock = new BasicBlock();

        // Делим инструкции
        nextBlock.Instructions = firstBlock.Instructions.Where(i => i.Offset >= offset).ToList();
        firstBlock.Instructions = firstBlock.Instructions.Where(i => i.Offset < offset).ToList();

        // Обновляем адреса
        nextBlock.EndOffset = firstBlock.EndOffset;
        firstBlock.EndOffset = firstBlock.Instructions[^1].Offset;
        nextBlock.StartOffset = nextBlock.Instructions[0].Offset;

        if (nextBlock.StartOffset != offset)
        {
            // Произошел переход не на инструкцию, а в середину инструкции
            throw new Exception();
        }

        // Переносим переходы
        nextBlock.Condition = firstBlock.Condition;
        nextBlock.ConditionalOffset = firstBlock.ConditionalOffset;
        nextBlock.ConditionalBlock = firstBlock.ConditionalBlock;
        nextBlock.NextBlock = firstBlock.NextBlock;

        firstBlock.Condition = null;
        firstBlock.ConditionalOffset = null;
        firstBlock.ConditionalBlock = null;

        firstBlock.NextBlock = firstBlock;

        Blocks.Add(nextBlock);

        return nextBlock;
    }
}
using System.Diagnostics;

namespace UltraDecompiler.Graph;

/// <summary>
/// Граф потока управления (Control Flow Graph)
/// </summary>
public class ControlFlowGraph
{
    private Dictionary<int, BasicBlock>? _blockByOffset = null;

    public List<BasicBlock> Blocks { get; private set; } = [];

    public BasicBlock EntryBlock { get; private set; } = null!;

    public void Build(X86Disassembler disassembler, int startOffset)
    {
        Build(disassembler, startOffset, RegisterState.Unknown);
    }

    /// <summary>
    /// Строит CFG из списка инструкций
    /// </summary>
    public void Build(X86Disassembler disassembler, int startOffset, RegisterState initRegisters)
    {
        Queue<(int, RegisterState)> queue = [];
        queue.Enqueue((startOffset, initRegisters));

        HashSet<int> visited = [];

        while (queue.Count > 0)
        {
            var (offset, registers) = queue.Dequeue();
            if (visited.Contains(offset))
                continue;

            if (offset < 0 || offset >= disassembler.Image.Length)
                continue;

            var block = new BasicBlock
            {
                StartOffset = offset
            };

            Debug.Assert(!Blocks.Any(b => b.StartOffset == offset));
            Blocks.Add(block);
            EntryBlock ??= block;

            bool isBreak = false;
            foreach (var instr in disassembler.DisassembleBranch(offset, registers))
            {
                if (visited.Contains(instr.Offset))
                {
                    // Середина блока уже была обработана. Прерываем обработку и связываем со следующим блоком
                    var nextBlock = Blocks.FirstOrDefault(b => b.StartOffset == instr.Offset);
                    if (nextBlock == null)
                    {
                        throw new Exception();
                    }

                    block.NextBlock = nextBlock;
                    isBreak = true;
                    break;
                }

                block.Instructions.Add(instr);
                visited.Add(instr.Offset);

                if (instr.IsCall)
                {
                    // TODO Нужна проверка, что этот в этом call есть return, т.к. там может быть выход через int 21 AH=4c
                    // Как вариант рекурсивный анализ
                }
            }

            if (isBreak)
                continue;

            var lastInstr = block.Instructions[^1];
            block.EndOffset = lastInstr.Offset;

            if (lastInstr.IsConditionalJump || lastInstr.IsUnconditionalJump)
            {
                var jumpAddr = lastInstr.GetEffectiveJumpTarget(disassembler.Image);

                // Проверка, что адрес еще не был обработан
                if (!visited.Contains(jumpAddr))
                    queue.Enqueue((jumpAddr, lastInstr.Registers));

                if (lastInstr.IsConditionalJump)
                {
                    var next = lastInstr.Offset + lastInstr.Size;
                    block.NextOffset = next;
                    block.ConditionalOffset = jumpAddr;

                    if (!visited.Contains(next))
                        queue.Enqueue((next, lastInstr.Registers));
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
    /// Строит CFG по заранее извлечённому телу одной функции.
    /// </summary>
    public void BuildFromInstructions(
        IReadOnlyList<Instruction> functionInstructions,
        int startOffset,
        byte[] image,
        RegisterState initRegisters)
    {
        ArgumentNullException.ThrowIfNull(functionInstructions);
        ArgumentNullException.ThrowIfNull(image);

        if (functionInstructions.Count == 0)
        {
            throw new ArgumentException("Пустое тело функции.", nameof(functionInstructions));
        }

        var byOffset = functionInstructions.ToDictionary(static i => i.Offset);
        var allowed = byOffset.Keys.ToHashSet();

        Queue<(int Offset, RegisterState Registers)> queue = [];
        queue.Enqueue((startOffset, initRegisters));

        HashSet<int> visited = [];

        while (queue.Count > 0)
        {
            var (offset, registers) = queue.Dequeue();
            if (visited.Contains(offset) || !allowed.Contains(offset))
            {
                continue;
            }

            var block = new BasicBlock
            {
                StartOffset = offset,
            };

            Blocks.Add(block);
            EntryBlock ??= block;

            var isBreak = false;
            var current = offset;

            while (allowed.Contains(current))
            {
                if (visited.Contains(current))
                {
                    var nextBlock = Blocks.FirstOrDefault(b => b.StartOffset == current)
                        ?? throw new InvalidOperationException($"CFG: блок 0x{current:X} не найден.");

                    block.NextBlock = nextBlock;
                    isBreak = true;
                    break;
                }

                var instr = byOffset[current];
                block.Instructions.Add(instr);
                visited.Add(current);
                registers = instr.Registers;

                if (instr.IsReturn || instr.IsExit)
                {
                    break;
                }

                if (instr.IsConditionalJump || instr.IsUnconditionalJump)
                {
                    var jumpAddr = instr.GetEffectiveJumpTarget(image);
                    if (jumpAddr != -1 && allowed.Contains(jumpAddr) && !visited.Contains(jumpAddr))
                    {
                        queue.Enqueue((jumpAddr, registers));
                    }

                    if (instr.IsConditionalJump)
                    {
                        var next = instr.Offset + instr.Size;
                        block.NextOffset = next;
                        block.ConditionalOffset = jumpAddr;

                        if (allowed.Contains(next) && !visited.Contains(next))
                        {
                            queue.Enqueue((next, registers));
                        }
                    }
                    else
                    {
                        block.NextOffset = jumpAddr;
                    }

                    break;
                }

                current = instr.Offset + instr.Size;
            }

            if (isBreak)
            {
                continue;
            }

            if (block.Instructions.Count == 0)
            {
                continue;
            }

            block.EndOffset = block.Instructions[^1].Offset;
        }

        BuildEdges();
    }

    /// <summary>
    /// Устанавливает взаимосвязи между блоками (NextBlock / ConditionalBlock).
    /// 
    /// Важно: внутри этого метода может происходить разбиение существующих блоков
    /// (см. GetBlock), поэтому вызов BuildPredecessors() должен быть строго после
    /// полного завершения BuildEdges().
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
        nextBlock.ConditionalOffset = firstBlock.ConditionalOffset;
        nextBlock.ConditionalBlock = firstBlock.ConditionalBlock;
        nextBlock.NextBlock = firstBlock.NextBlock;

        firstBlock.ConditionalOffset = null;
        firstBlock.ConditionalBlock = null;

        firstBlock.NextBlock = nextBlock;

        Blocks.Add(nextBlock);
        _blockByOffset.Add(nextBlock.StartOffset, nextBlock);

        return nextBlock;
    }
}
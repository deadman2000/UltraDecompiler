using System.Diagnostics;

namespace UltraDecompiler.Disassembly.Graph;

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

            var lastInstr = block.Instructions[^1];
            block.EndOffset = lastInstr.Offset;

            if (isBreak)
                continue;

            if (lastInstr.IsConditionalJump || lastInstr.IsUnconditionalJump)
            {
                var jumpAddr = lastInstr.JumpTarget;

                // Проверка, что адрес еще не был обработан
                if (jumpAddr != -1 && !visited.Contains(jumpAddr))
                    queue.Enqueue((jumpAddr, lastInstr.Registers));

                if (lastInstr.IsConditionalJump)
                {
                    var next = lastInstr.Offset + lastInstr.Size;
                    block.NextOffset = next;
                    block.ConditionalOffset = (jumpAddr != -1) ? jumpAddr : null;

                    if (!visited.Contains(next))
                        queue.Enqueue((next, lastInstr.Registers));
                }
                else
                {
                    block.NextOffset = (jumpAddr != -1) ? jumpAddr : null;
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
        RegisterState initRegisters)
    {
        ArgumentNullException.ThrowIfNull(functionInstructions);

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

            var current = offset;

            while (allowed.Contains(current))
            {
                if (visited.Contains(current))
                {
                    var nextBlock = Blocks.FirstOrDefault(b => b.StartOffset == current)
                        ?? throw new InvalidOperationException($"CFG: блок 0x{current:X} не найден.");

                    block.NextBlock = nextBlock;
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
                    var jumpAddr = instr.JumpTarget;
                    bool validJump = jumpAddr != -1 && allowed.Contains(jumpAddr);

                    if (validJump && !visited.Contains(jumpAddr))
                    {
                        queue.Enqueue((jumpAddr, registers));
                    }

                    if (instr.IsConditionalJump)
                    {
                        var next = instr.Offset + instr.Size;
                        block.NextOffset = next;
                        block.ConditionalOffset = validJump ? jumpAddr : null;

                        if (allowed.Contains(next) && !visited.Contains(next))
                        {
                            queue.Enqueue((next, registers));
                        }
                    }
                    else
                    {
                        block.NextOffset = validJump ? jumpAddr : null;
                    }

                    break;
                }

                current = instr.Offset + instr.Size;
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
        Debug.Assert(Blocks.All(b => b.EndOffset != -1));
        _blockByOffset = Blocks.ToDictionary(b => b.StartOffset);

        // Число блоков может меняться, поэтому используем for
        for (int i = 0; i < Blocks.Count; i++)
        {
            var block = Blocks[i];

            if (block.NextOffset.HasValue)
            {
                var nextTargetOffset = block.NextOffset.Value;
                var nextTarget = GetBlock(nextTargetOffset);
                if (block.NextOffset.HasValue)
                {
                    // Обычный случай: блок не был разбит при GetBlock (split случился на другом блоке)
                    block.NextBlock = nextTarget;
                    block.NextOffset = null;
                }
                // else: случился split этого блока (uncond jump в середину себя) — pending NextOffset перенесён в suffix,
                // sequential NextBlock уже проставлен внутри GetBlock на prefix; suffix обработает свой pending в последующих итерациях for
            }

            if (block.ConditionalOffset.HasValue)
            {
                var conditionalOffset = block.ConditionalOffset.Value;
                var conditionalTarget = GetBlock(conditionalOffset);
                if (block.ConditionalOffset.HasValue)
                {
                    // Обычный случай: jumper-блок не был разбит
                    block.ConditionalBlock = conditionalTarget;
                    block.ConditionalOffset = null;
                }
                // else: GetBlock разбивает текущий блок (cond jump в середину себя) — pending ConditionalOffset перенесён
                // в suffix-блок (который теперь содержит jump-инстр и стартует с conditional target); он будет обработан позже в цикле for
            }
        }

        _blockByOffset = null;
        Debug.Assert(Blocks.All(b => b.EndOffset != -1));
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
            // Target jump за пределами текущей процедуры или на не-инструкцию (редко, но возможно для tail-jmp в crt0).
            // Не фатально — NextBlock/ConditionalBlock останется null.
            Debug.WriteLine($"CFG: block target not found in current proc: 0x{offset:X4}");
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

        // Переносим переходы (в т.ч. NextOffset, если блок с прыжком был разбит; pending всегда уходят в suffix,
        // т.к. при split внутри блока с terminating jump'ом сам jump-инстр уходит в nextBlock)
        nextBlock.NextOffset = firstBlock.NextOffset;
        nextBlock.ConditionalOffset = firstBlock.ConditionalOffset;
        nextBlock.ConditionalBlock = firstBlock.ConditionalBlock;
        nextBlock.NextBlock = firstBlock.NextBlock;

        firstBlock.NextOffset = null;
        firstBlock.ConditionalOffset = null;
        firstBlock.ConditionalBlock = null;

        firstBlock.NextBlock = nextBlock;

        Blocks.Add(nextBlock);
        _blockByOffset.Add(nextBlock.StartOffset, nextBlock);

        return nextBlock;
    }
}
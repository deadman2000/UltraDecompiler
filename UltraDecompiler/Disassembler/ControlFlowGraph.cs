namespace UltraDecompiler.Disassembler;

/// <summary>
/// Граф потока управления (Control Flow Graph)
/// </summary>
public class ControlFlowGraph
{
    public List<BasicBlock> Blocks { get; private set; } = new();

    public BasicBlock EntryBlock { get; private set; }

    /// <summary>
    /// Строит CFG из списка инструкций
    /// </summary>
    public static ControlFlowGraph Build(List<Instruction> instructions)
    {
        var cfg = new ControlFlowGraph();
        if (instructions == null || instructions.Count == 0)
            return cfg;

        // 1. Находим все адреса, на которые есть переходы (лидеры)
        var leaders = new HashSet<int> { instructions[0].Offset }; // первый блок всегда лидер

        for (int i = 0; i < instructions.Count; i++)
        {
            var instr = instructions[i];

            // Условные и безусловные переходы создают новый лидер
            if (instr.IsJump || instr.Mnemonic is Mnemonic.CALL or Mnemonic.RET or Mnemonic.RETF)
            {
                if (i + 1 < instructions.Count)
                    leaders.Add(instructions[i + 1].Offset);

                // Целевой адрес перехода тоже лидер
                if (TryGetJumpTarget(instr, out int target))
                    leaders.Add(target);
            }
        }

        // 2. Разбиваем инструкции на базовые блоки
        var currentBlock = new BasicBlock();
        cfg.Blocks.Add(currentBlock);
        cfg.EntryBlock = currentBlock;

        foreach (var instr in instructions)
        {
            if (leaders.Contains(instr.Offset) && currentBlock.Instructions.Count > 0)
            {
                currentBlock.EndOffset = currentBlock.Instructions.Last().Offset;
                currentBlock = new BasicBlock { StartOffset = instr.Offset };
                cfg.Blocks.Add(currentBlock);
            }

            currentBlock.Instructions.Add(instr);
        }

        if (currentBlock.Instructions.Count > 0)
            currentBlock.EndOffset = currentBlock.Instructions.Last().Offset;

        // 3. Строим связи между блоками
        BuildEdges(cfg, instructions);

        return cfg;
    }

    private static void BuildEdges(ControlFlowGraph cfg, List<Instruction> allInstructions)
    {
        var offsetToBlock = cfg.Blocks.ToDictionary(b => b.StartOffset, b => b);

        foreach (var block in cfg.Blocks)
        {
            if (block.Instructions.Count == 0) continue;

            var lastInstr = block.Instructions.Last();

            // Безусловный переход
            if (lastInstr.Mnemonic == Mnemonic.JMP)
            {
                if (TryGetJumpTarget(lastInstr, out int target) && offsetToBlock.TryGetValue(target, out var targetBlock))
                {
                    block.Successors.Add(targetBlock);
                    targetBlock.Predecessors.Add(block);
                }
            }
            // Условный переход
            else if (lastInstr.IsJump)
            {
                // Переход по условию
                if (TryGetJumpTarget(lastInstr, out int target) && offsetToBlock.TryGetValue(target, out var targetBlock))
                {
                    block.Successors.Add(targetBlock);
                    targetBlock.Predecessors.Add(block);
                }

                // Падение на следующий блок (если условие не сработало)
                int nextOffset = lastInstr.Offset + lastInstr.Bytes.Length;
                if (offsetToBlock.TryGetValue(nextOffset, out var nextBlock))
                {
                    block.Successors.Add(nextBlock);
                    nextBlock.Predecessors.Add(block);
                }
            }
            // CALL — переходим на следующий блок после вызова
            else if (lastInstr.Mnemonic == Mnemonic.CALL)
            {
                int nextOffset = lastInstr.Offset + lastInstr.Bytes.Length;
                if (offsetToBlock.TryGetValue(nextOffset, out var nextBlock))
                {
                    block.Successors.Add(nextBlock);
                    nextBlock.Predecessors.Add(block);
                }
            }
            // RET / RETF — конец функции
            else if (lastInstr.Mnemonic is Mnemonic.RET or Mnemonic.RETF)
            {
                // Не добавляем successors
            }
            // Обычная инструкция — просто падаем на следующий блок
            else
            {
                int nextOffset = lastInstr.Offset + lastInstr.Bytes.Length;
                if (offsetToBlock.TryGetValue(nextOffset, out var nextBlock))
                {
                    block.Successors.Add(nextBlock);
                    nextBlock.Predecessors.Add(block);
                }
            }
        }
    }

    private static bool TryGetJumpTarget(Instruction instr, out int target)
    {
        target = 0;
        if (string.IsNullOrEmpty(instr.Operands))
            return false;

        // Простой парсинг адреса из вида "0x1234"
        var parts = instr.Operands.Split(new[] { ' ', ',', ':' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith("0x") && int.TryParse(part[2..], System.Globalization.NumberStyles.HexNumber, null, out int addr))
            {
                target = addr;
                return true;
            }
        }
        return false;
    }

    public void Print()
    {
        Console.WriteLine("=== Control Flow Graph ===");
        foreach (var block in Blocks)
        {
            Console.WriteLine(block);
            if (block.Successors.Count > 0)
            {
                Console.WriteLine("  → " + string.Join(", ", block.Successors.Select(b => $"0x{b.StartOffset:X6}")));
            }
        }
    }
}
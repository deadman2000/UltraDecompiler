using UltraDecompiler.Graph;

namespace UltraDecompiler.Decompilation;

/// <summary>
/// Распознаёт switch Microsoft QuickC по ассемблерному шаблону.
/// </summary>
/// <remarks>
/// QuickC генерирует (см. <c>QuickC/PROGRAMS/switch.c</c>):
/// <list type="number">
/// <item><description><c>mov REG, [var]; jmp dispatcher</c> — вход в switch;</description></item>
/// <item><description>тела case выше диспетчера, каждое заканчивается <c>jmp merge</c>;</description></item>
/// <item><description>диспетчер: <c>cmp REG, c; jne next; jmp case</c> (обратный jmp);</description></item>
/// <item><description>хвост: <c>jmp default</c> (обратный jmp).</description></item>
/// </list>
/// Ручная цепочка <c>if (x != c)</c> использует <c>cmp [mem], imm</c> и прямые jmp в case — не совпадает.
/// </remarks>
public static class QuickCSwitchDetector
{
    /// <summary>Ищет все switch-паттерны QuickC в CFG процедуры.</summary>
    public static IReadOnlyList<QuickCSwitchPattern> Detect(IReadOnlyList<BasicBlock> blocks)
    {
        if (blocks.Count == 0)
        {
            return [];
        }

        var blockByOffset = blocks.ToDictionary(static b => b.StartOffset);
        var instructionByOffset = BuildInstructionMap(blocks);
        var results = new List<QuickCSwitchPattern>();
        var claimedDispatcherStarts = new HashSet<int>();

        foreach (var block in blocks.OrderBy(static b => b.StartOffset))
        {
            foreach (var instruction in block.Instructions)
            {
                if (!TryParseCmpRegImm(instruction, out _, out _))
                {
                    continue;
                }

                if (!TryParseDispatcherChain(
                        instruction.Offset,
                        instructionByOffset,
                        blockByOffset,
                        out var register,
                        out var cases,
                        out var defaultTarget,
                        out var dispatcherBlockOffsets))
                {
                    continue;
                }

                if (!claimedDispatcherStarts.Add(instruction.Offset))
                {
                    continue;
                }

                if (!TryFindSwitchEntry(
                        instruction.Offset,
                        register,
                        blockByOffset,
                        out var entryOffset))
                {
                    continue;
                }

                if (!TryResolveMergeOffset(
                        cases,
                        defaultTarget,
                        entryOffset,
                        instruction.Offset,
                        blockByOffset,
                        out var mergeOffset))
                {
                    continue;
                }

                results.Add(new QuickCSwitchPattern
                {
                    EntryOffset = entryOffset,
                    DispatcherStart = instruction.Offset,
                    MergeOffset = mergeOffset,
                    DiscriminantRegister = register,
                    Cases = cases
                        .Select(static c => new QuickCSwitchCasePattern(new ConstExpr(c.Value), c.BodyStartOffset))
                        .ToList(),
                    DefaultBodyOffset = defaultTarget,
                    DispatcherBlockOffsets = dispatcherBlockOffsets,
                });
            }
        }

        return results;
    }

    private static Dictionary<int, Instruction> BuildInstructionMap(IReadOnlyList<BasicBlock> blocks)
    {
        var map = new Dictionary<int, Instruction>();
        foreach (var block in blocks)
        {
            foreach (var instruction in block.Instructions)
            {
                map[instruction.Offset] = instruction;
            }
        }

        return map;
    }

    private static bool TryFindSwitchEntry(
        int dispatcherStart,
        GpRegister16 register,
        IReadOnlyDictionary<int, BasicBlock> blockByOffset,
        out int entryOffset)
    {
        entryOffset = -1;

        foreach (var block in blockByOffset.Values.OrderBy(static b => b.StartOffset))
        {
            for (var i = 0; i < block.Instructions.Count - 1; i++)
            {
                var mov = block.Instructions[i];
                var jmp = block.Instructions[i + 1];

                if (!TryParseMovRegFromMemory(mov, out var movRegister)
                    || movRegister != register
                    || jmp.Mnemonic != Mnemonic.JMP
                    || jmp.JumpTarget != dispatcherStart
                    || jmp.JumpTarget <= mov.Offset)
                {
                    continue;
                }

                entryOffset = block.StartOffset;
                return true;
            }
        }

        return false;
    }

    private static bool TryParseDispatcherChain(
        int startOffset,
        IReadOnlyDictionary<int, Instruction> instructionByOffset,
        IReadOnlyDictionary<int, BasicBlock> blockByOffset,
        out GpRegister16 register,
        out List<(int Value, int BodyStartOffset)> cases,
        out int defaultTarget,
        out List<int> dispatcherBlockOffsets)
    {
        register = default;
        cases = [];
        defaultTarget = -1;
        dispatcherBlockOffsets = [];

        var offset = startOffset;
        int? previousValue = null;

        while (instructionByOffset.TryGetValue(offset, out var cmpInstruction))
        {
            if (!TryParseCmpRegImm(cmpInstruction, out var cmpRegister, out var constant))
            {
                break;
            }

            if (cases.Count == 0)
            {
                register = cmpRegister;
            }
            else if (register != cmpRegister)
            {
                return false;
            }

            if (previousValue is int previous && constant <= previous)
            {
                return false;
            }

            previousValue = constant;

            if (!TryGetNextInstruction(cmpInstruction, instructionByOffset, out var jneInstruction)
                || jneInstruction.Mnemonic != Mnemonic.JNE
                || jneInstruction.JumpTarget <= cmpInstruction.Offset)
            {
                return false;
            }

            if (!TryGetNextInstruction(jneInstruction, instructionByOffset, out var caseJumpInstruction)
                || caseJumpInstruction.Mnemonic != Mnemonic.JMP
                || caseJumpInstruction.JumpTarget >= cmpInstruction.Offset)
            {
                return false;
            }

            TrackDispatcherBlock(dispatcherBlockOffsets, blockByOffset, cmpInstruction.Offset);

            cases.Add((constant, caseJumpInstruction.JumpTarget));
            offset = jneInstruction.JumpTarget;
        }

        if (cases.Count == 0
            || !instructionByOffset.TryGetValue(offset, out var defaultJumpInstruction)
            || defaultJumpInstruction.Mnemonic != Mnemonic.JMP
            || defaultJumpInstruction.JumpTarget >= defaultJumpInstruction.Offset)
        {
            return false;
        }

        TrackDispatcherBlock(dispatcherBlockOffsets, blockByOffset, defaultJumpInstruction.Offset);
        defaultTarget = defaultJumpInstruction.JumpTarget;
        return true;
    }

    private static void TrackDispatcherBlock(
        List<int> dispatcherBlockOffsets,
        IReadOnlyDictionary<int, BasicBlock> blockByOffset,
        int instructionOffset)
    {
        foreach (var block in blockByOffset.Values)
        {
            if (block.StartOffset <= instructionOffset && instructionOffset <= block.EndOffset)
            {
                if (dispatcherBlockOffsets.Count == 0 || dispatcherBlockOffsets[^1] != block.StartOffset)
                {
                    dispatcherBlockOffsets.Add(block.StartOffset);
                }

                break;
            }
        }
    }

    private static bool TryResolveMergeOffset(
        IReadOnlyList<(int Value, int BodyStartOffset)> cases,
        int defaultTarget,
        int entryOffset,
        int dispatcherStart,
        IReadOnlyDictionary<int, BasicBlock> blockByOffset,
        out int mergeOffset)
    {
        mergeOffset = -1;
        int? expectedMerge = null;

        foreach (var bodyStart in cases.Select(static c => c.BodyStartOffset).Append(defaultTarget))
        {
            if (bodyStart <= entryOffset || bodyStart >= dispatcherStart)
            {
                return false;
            }

            if (!blockByOffset.TryGetValue(bodyStart, out var bodyBlock)
                || !TryFindCaseMergeOffset(bodyBlock, blockByOffset, dispatcherStart, out var candidateMerge))
            {
                return false;
            }

            expectedMerge ??= candidateMerge;
            if (expectedMerge != candidateMerge)
            {
                return false;
            }
        }

        mergeOffset = expectedMerge!.Value;
        return mergeOffset > dispatcherStart;
    }

    private static bool TryFindCaseMergeOffset(
        BasicBlock bodyBlock,
        IReadOnlyDictionary<int, BasicBlock> blockByOffset,
        int dispatcherStart,
        out int mergeOffset)
    {
        mergeOffset = -1;
        var current = bodyBlock;
        var visited = new HashSet<int>();

        while (current is not null && visited.Add(current.StartOffset))
        {
            var lastInstruction = current.Instructions[^1];
            if (lastInstruction.IsUnconditionalJump && lastInstruction.JumpTarget > dispatcherStart)
            {
                mergeOffset = lastInstruction.JumpTarget;
                return true;
            }

            if (lastInstruction.IsUnconditionalJump
                && blockByOffset.TryGetValue(lastInstruction.JumpTarget, out var jumpTargetBlock))
            {
                current = jumpTargetBlock;
                continue;
            }

            current = current.NextBlock;
        }

        return false;
    }

    private static bool TryGetNextInstruction(
        Instruction current,
        IReadOnlyDictionary<int, Instruction> instructionByOffset,
        out Instruction nextInstruction)
    {
        var nextOffset = current.Offset + current.Size;
        return instructionByOffset.TryGetValue(nextOffset, out nextInstruction!);
    }

    private static bool TryParseCmpRegImm(Instruction instruction, out GpRegister16 register, out int immediate)
    {
        register = default;
        immediate = 0;

        if (instruction.Mnemonic != Mnemonic.CMP
            || instruction.Operand1.Type != OperandType.Register16
            || instruction.Operand2.Type != OperandType.Immediate16)
        {
            return false;
        }

        register = instruction.Operand1.AsGpRegister16();
        immediate = instruction.Operand2.Value;
        return true;
    }

    private static bool TryParseMovRegFromMemory(Instruction instruction, out GpRegister16 register)
    {
        register = default;

        if (instruction.Mnemonic != Mnemonic.MOV
            || instruction.Operand1.Type != OperandType.Register16
            || instruction.Operand2.Type != OperandType.Memory)
        {
            return false;
        }

        register = instruction.Operand1.AsGpRegister16();
        return true;
    }
}

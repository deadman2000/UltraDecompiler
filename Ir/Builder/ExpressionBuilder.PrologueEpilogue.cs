using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.Builder;

/// <summary>
/// Специальная обработка пролога и эпилога функций QuickC.
/// Инструкции пролога/эпилога не создают IR-операций, а только обновляют
/// символическое состояние регистров и стека.
/// </summary>
public partial class ExpressionBuilder
{
    /// <summary>
    /// Диапазон инструкций пролога (start включительно, end исключительно).
    /// </summary>
    private readonly record struct PrologueRange(int Start, int End);

    /// <summary>
    /// Определяет диапазон инструкций стандартного пролога QuickC в начале блока.
    /// Распознаёт:
    /// - <c>push bp</c>
    /// - <c>mov bp, sp</c>
    /// - <c>sub sp, N</c> (выделение стека для локалов)
    /// - <c>push reg</c> (сохранение регистров, опционально)
    /// </summary>
    private static PrologueRange? GetPrologueRange(IReadOnlyList<Instruction> instructions)
    {
        if (instructions.Count == 0)
            return null;

        // Проверяем наличие стандартного пролога
        if (!PrologueDetector.HasStandardPrologue(instructions))
            return null;

        int end;

        // Пропускаем prologue detector-распознанную часть (push bp + mov bp, sp или enter)
        if (instructions[0].Mnemonic == Mnemonic.ENTER)
        {
            end = 1;
        }
        else
        {
            // push bp; mov bp, sp
            if (instructions.Count >= 2
                && instructions[0].Mnemonic == Mnemonic.PUSH
                && instructions[0].Operand1.Type == OperandType.Register16
                && instructions[0].Operand1.AsGpRegister16() == GpRegister16.BP
                && instructions[1].Mnemonic == Mnemonic.MOV
                && instructions[1].Operand1.Type == OperandType.Register16
                && instructions[1].Operand1.AsGpRegister16() == GpRegister16.BP
                && instructions[1].Operand2.Type == OperandType.Register16
                && instructions[1].Operand2.AsGpRegister16() == GpRegister16.SP)
            {
                end = 2;
            }
            else
            {
                return null;
            }
        }

        // После базового пролога могут идти:
        // - sub sp, N (выделение места для локалов)
        // - push reg (сохранение регистров)
        while (end < instructions.Count)
        {
            var instr = instructions[end];

            // sub sp, imm16 — выделение стека для локальных переменных
            if (instr.Mnemonic == Mnemonic.SUB
                && instr.Operand1.Type == OperandType.Register16
                && instr.Operand1.AsGpRegister16() == GpRegister16.SP
                && instr.Operand2.Type == OperandType.Immediate16)
            {
                end++;
                continue;
            }

            // push reg16 — сохранение регистров (кроме BP, который уже сохранён)
            if (instr.Mnemonic == Mnemonic.PUSH
                && instr.Operand1.Type == OperandType.Register16
                && instr.Operand1.AsGpRegister16() != GpRegister16.BP)
            {
                end++;
                continue;
            }

            // Больше ничего не распознаём как часть пролога
            break;
        }

        return end > 0 ? new PrologueRange(0, end) : null;
    }

    /// <summary>
    /// Обрабатывает инструкцию пролога без создания IR-операций.
    /// Только обновляет символическое состояние регистров.
    /// </summary>
    private void HandlePrologueInstruction(ExprBlock block, Instruction instr)
    {
        switch (instr.Mnemonic)
        {
            case Mnemonic.PUSH:
                // push bp / push reg — только символический стек, без IR-операций
                var pushedExpr = instr.Operand1.GetExpression(block, instr.Segment);
                block.EndStack.Push(pushedExpr);
                block.PushExprsByOffset[instr.Offset] = pushedExpr;
                break;

            case Mnemonic.MOV:
            case Mnemonic.SUB:
                // mov bp, sp / sub sp, N — служебная настройка кадра, IR не нужен
                break;

            case Mnemonic.ENTER:
                // enter N, 0 — аналог push bp; mov bp, sp; sub sp, N
                if (instr.Operand2.Type == OperandType.Immediate8 && instr.Operand2.Value == 0)
                {
                    block.EndStack.Push(block.Variables.BP.ToGet());
                    block.PushExprsByOffset[instr.Offset] = block.Variables.BP.ToGet();
                }
                break;
        }
    }

    /// <summary>
    /// Диапазон инструкций эпилога (start включительно, end исключительно).
    /// </summary>
    private readonly record struct EpilogueRange(int Start, int End);

    /// <summary>
    /// Определяет диапазон инструкций эпилога в конце блока.
    /// Распознаёт:
    /// - <c>mov sp, bp</c>
    /// - <c>pop bp</c>
    /// - <c>leave</c>
    /// - <c>pop reg</c> (восстановление регистров)
    /// - <c>ret</c> / <c>ret N</c> (завершает блок)
    /// </summary>
    private static EpilogueRange? GetEpilogueRange(IReadOnlyList<Instruction> instructions)
    {
        if (instructions.Count == 0)
            return null;

        // Ищем начало эпилога с конца
        int start = instructions.Count;

        // Пропускаем ret в конце (он обрабатывается отдельно)
        if (start > 0 && instructions[start - 1].IsReturn)
        {
            start--;
        }

        // Двигаемся назад, распознавая инструкции эпилога
        while (start > 0)
        {
            var instr = instructions[start - 1];

            if (IsEpilogueInstruction(instr))
            {
                start--;
            }
            else
            {
                break;
            }
        }

        // Если ничего не нашли — эпилога нет
        if (start >= instructions.Count - (instructions.Any(i => i.IsReturn) ? 1 : 0))
            return null;

        return new EpilogueRange(start, instructions.Count - (instructions.Any(i => i.IsReturn) ? 1 : 0));
    }

    /// <summary>
    /// Обрабатывает инструкцию эпилога без создания IR-операций.
    /// Только обновляет символическое состояние регистров.
    /// </summary>
    private void HandleEpilogueInstruction(ExprBlock block, Instruction instr)
    {
        switch (instr.Mnemonic)
        {
            case Mnemonic.POP:
                // pop bp / pop reg — только символический стек, без IR-операций
                if (block.EndStack.Count > 0)
                {
                    block.EndStack.Pop();
                }
                break;

            case Mnemonic.MOV:
            case Mnemonic.JMP:
                // mov sp, bp / jmp в общий эпилог — служебные инструкции
                break;

            case Mnemonic.LEAVE:
                // leave = mov sp, bp; pop bp
                if (block.EndStack.Count > 0)
                {
                    block.EndStack.Pop();
                }
                break;
        }
    }

    /// <summary>
    /// Проверяет, является ли инструкция частью эпилога (восстановление регистров перед RET).
    /// </summary>
    private static bool IsEpilogueInstruction(Instruction instr) =>
        instr.Mnemonic switch
        {
            Mnemonic.POP => true,
            Mnemonic.MOV when instr.Operand1.Type == OperandType.Register16
                && instr.Operand1.AsGpRegister16() == GpRegister16.SP
                && instr.Operand2.Type == OperandType.Register16
                && instr.Operand2.AsGpRegister16() == GpRegister16.BP => true,
            Mnemonic.LEAVE => true,
            Mnemonic.JMP => true,
            _ => false,
        };

    /// <summary>
    /// Блок содержит только инструкции эпилога и завершается RET.
    /// </summary>
    private static bool IsEpilogueTailBlock(IReadOnlyList<Instruction> instructions)
    {
        var hasRet = false;

        foreach (var instr in instructions)
        {
            if (instr.IsReturn)
            {
                hasRet = true;
                continue;
            }

            if (!IsEpilogueInstruction(instr))
            {
                return false;
            }
        }

        return hasRet;
    }

    /// <summary>
    /// Блок эпилога, на который есть безусловный JMP — return добавляется
    /// в блоке-источнике перехода (/Od).
    /// </summary>
    protected bool IsSharedEpilogueReachedByTailJmp(BasicBlock block)
    {
        if (!IsEpilogueTailBlock(block.Instructions))
        {
            return false;
        }

        if (!_predecessors.TryGetValue(block, out var predecessors))
        {
            return false;
        }

        var startOffset = block.StartOffset;
        foreach (var pred in predecessors)
        {
            if (pred.Instructions.Count == 0)
            {
                continue;
            }

            var last = pred.Instructions[^1];
            if (last.IsUnconditionalJump && last.JumpTarget == startOffset)
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

    /// <summary>
    /// Для /Od: заменяет tail jmp в общий эпилог явным <see cref="ReturnOperation"/> с текущим AX.
    /// </summary>
    private void InsertTailReturnsBeforeEpilogue(ControlFlowGraph graph)
    {
        if (!ShouldInsertTailReturnsBeforeEpilogue())
        {
            return;
        }

        var blockByOffset = graph.Blocks.ToDictionary(static b => b.StartOffset);

        foreach (var block in Blocks)
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

            if (!IsEpilogueTailBlock(targetBlock.Instructions))
            {
                continue;
            }

            var isExplicit = !IsNaturalEpilogueMerge(block.BasicBlock, targetOffset);
            block.Operations.Add(new ReturnOperation(block.Variables.AX.ToGet(), IsExplicit: isExplicit));
        }
    }

    /// <summary>
    /// JMP в эпилог сразу после ветки, где jcc тоже ведёт в тот же эпилог — неявный выход (пустой if).
    /// </summary>
    private bool IsNaturalEpilogueMerge(BasicBlock jmpBlock, int epilogueOffset)
    {
        if (!_predecessors.TryGetValue(jmpBlock, out var predecessors))
        {
            return false;
        }

        foreach (var pred in predecessors)
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

    /// <summary>
    /// Удаляет из IR-дерева блоки общего эпилога QuickC /Od (pop/leave/ret),
    /// на которые ведёт tail jmp. Return уже стоит в блоке-источнике перехода.
    /// </summary>
    private void RemoveSharedEpilogueBlocks()
    {
        var epilogueBlocks = Blocks
            .Where(b => IsSharedEpilogueReachedByTailJmp(b.BasicBlock))
            .ToList();

        foreach (var epilogue in epilogueBlocks)
        {
            foreach (var block in Blocks)
            {
                if (ReferenceEquals(block.Next, epilogue))
                {
                    block.Next = null;
                }

                if (ReferenceEquals(block.ConditionalBlock, epilogue))
                {
                    block.ConditionalBlock = null;
                }
            }

            Blocks.Remove(epilogue);
            _blocksMap.Remove(epilogue.BasicBlock);
        }
    }
}

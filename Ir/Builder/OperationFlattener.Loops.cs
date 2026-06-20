using UltraDecompiler.Ir.Builder.Loops;

namespace UltraDecompiler.Ir.Builder;

/// <summary>
/// Содержит логику распознавания и эмиссии циклов QuickC.
/// </summary>
public partial class OperationFlattener
{
    /// <summary>
    /// Определяет, следует ли конвертировать заголовок цикла в WhileOperation или ForOperation.
    /// Делегирует анализ специализированному анализатору циклов.
    /// </summary>
    protected virtual bool ShouldConvertLoopHeader(ExprBlock block, ExprBlock? enclosingLoopExit = null,
        ExprBlock? enclosingLoopHeader = null)
    {
        // Используем анализатор
        if (_loopAnalyzer.IsLoopHeader(block, enclosingLoopExit, enclosingLoopHeader))
            return true;

        // Fallback: старая эвристика для сложных случаев
        if (block.Condition is null)
            return false;

        // Для unopt do-while test block: имеет conditional + uncond jmp назад в instrs.
        var ins = block.BasicBlock.Instructions;
        if (ins.Any(i => i.IsConditionalJump) && ins.Any(i => i.IsUnconditionalJump))
        {
            return true;
        }

        // while по указателю: условие проверяет разыменованный указатель
        if (ConditionUsesCharPointerDeref(block.Condition))
        {
            // Проверяем, что это не break (обе ветки должны вести к заголовку или merge)
            if (block.Next != null && block.ConditionalBlock != null)
            {
                // Это похоже на while - конвертируем
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Проверяет, является ли блок break/continue внутри цикла (не заголовок).
    /// </summary>
    public bool IsBreakOrContinueBlock(ExprBlock block, ExprBlock loopHeader)
    {
        if (block.Condition is null || ReferenceEquals(block, loopHeader))
            return false;

        // break/continue блок имеет только ОДНУ ветку, ведущую назад к заголовку
        var nextReaches = block.Next != null && ReachesFrom(block.Next, loopHeader);
        var conditionalReaches = block.ConditionalBlock != null && ReachesFrom(block.ConditionalBlock, loopHeader);

        // Если только одна ветка ведёт к заголовку - это break/continue
        return (nextReaches && !conditionalReaches) || (!nextReaches && conditionalReaches);
    }

    /// <summary>
    /// Проверяет, является ли условие циклом по argc (такие циклы не конвертируем).
    /// </summary>
    public static bool IsArgcBoundLoopHeader(Expr condition) =>
        condition is CmpExpr { Operation: CmpOperation.Uge or CmpOperation.Ugt, Right: Variable { Name: "argc" } }
        || condition is CmpExpr { Operation: CmpOperation.Uge or CmpOperation.Ugt, Left: Variable { Name: "argc" } };

    /// <summary>
    /// Определяет раскладку цикла: тело, выход, условие продолжения.
    /// </summary>
    private LoopLayout? TryGetLoopLayout(ExprBlock headerBlock)
    {
        if (headerBlock.Condition is null)
            return null;

        // Проверка на do-while: заголовок имеет cond + uncond jmp
        var instrs = headerBlock.BasicBlock.Instructions;
        bool isDoWhile = instrs.Any(i => i.IsConditionalJump) && instrs.Any(i => i.IsUnconditionalJump);

        if (isDoWhile)
        {
            // Для do-while: тело - это блоки ПЕРЕД заголовком по offset
            var bodyStart = FindDoWhileBodyStart(headerBlock);
            if (bodyStart != null)
            {
                // exitBlock - это fallthrough после условного перехода
                return new LoopLayout(bodyStart, headerBlock.Next, !headerBlock.Condition);
            }
        }

        // Паттерн 1: fallthrough (Next) ведёт в тело
        if (headerBlock.Next is not null)
        {
            var bodyEntry = ResolveLoopBodyStart(headerBlock.Next);
            if (bodyEntry != null && ReachesFrom(bodyEntry, headerBlock))
            {
                return new LoopLayout(bodyEntry, headerBlock.ConditionalBlock, !headerBlock.Condition);
            }
        }

        // Паттерн 2: conditional ветка ведёт в тело
        if (headerBlock.ConditionalBlock is not null)
        {
            var bodyEntry = ResolveLoopBodyStart(headerBlock.ConditionalBlock);
            if (bodyEntry != null && ReachesFrom(bodyEntry, headerBlock))
            {
                return new LoopLayout(bodyEntry, headerBlock.Next, headerBlock.Condition);
            }
        }

        // Паттерн 3: Next ведёт в тело (без обратной проверки)
        if (headerBlock.Next is not null)
        {
            var bodyEntry = ResolveLoopBodyStart(headerBlock.Next);
            if (bodyEntry != null)
            {
                return new LoopLayout(bodyEntry, headerBlock.ConditionalBlock, !headerBlock.Condition);
            }
        }

        // Паттерн 4: conditional ведёт в тело (без обратной проверки)
        if (headerBlock.ConditionalBlock is not null)
        {
            var bodyEntry = ResolveLoopBodyStart(headerBlock.ConditionalBlock);
            if (bodyEntry != null)
            {
                return new LoopLayout(bodyEntry, headerBlock.Next, headerBlock.Condition);
            }
        }

        // Паттерн 5: forward jmp на exit (while по указателю)
        // Если conditional ветка ведёт вперёд на exit, а Next ведёт в тело
        if (headerBlock.ConditionalBlock != null &&
            headerBlock.ConditionalBlock.BasicBlock.StartOffset > headerBlock.BasicBlock.StartOffset &&
            headerBlock.Next != null)
        {
            var bodyEntry = ResolveLoopBodyStart(headerBlock.Next);
            if (bodyEntry != null)
            {
                return new LoopLayout(bodyEntry, headerBlock.ConditionalBlock, !headerBlock.Condition);
            }
        }

        return null;
    }

    /// <summary>
    /// Ищет начало тела для do-while (блоки перед заголовком по offset).
    /// </summary>
    private ExprBlock? FindDoWhileBodyStart(ExprBlock headerBlock)
    {
        // Ищем блок, который jmp-ится на header и имеет операции
        foreach (var block in _builder.Blocks)
        {
            if (block.BasicBlock.StartOffset >= headerBlock.BasicBlock.StartOffset)
                continue;

            // Проверяем, ведёт ли этот блок на заголовок
            if (block.Next == headerBlock || block.ConditionalBlock == headerBlock)
            {
                // Это кандидат - блок, который ведёт на заголовок
                if (block.Operations.Count > 0)
                {
                    return block;
                }
            }
        }

        // Fallback: ищем ближайший блок перед заголовком, который имеет операции
        ExprBlock? bestCandidate = null;
        for (int i = _builder.Blocks.Count - 1; i >= 0; i--)
        {
            if (_builder.Blocks[i] == headerBlock)
            {
                return bestCandidate;
            }

            if (_builder.Blocks[i].Operations.Count > 0 && bestCandidate == null)
            {
                bestCandidate = _builder.Blocks[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Пропускает пустые jmp-блоки до реального входа в тело цикла.
    /// </summary>
    private static ExprBlock? ResolveLoopBodyStart(ExprBlock start)
    {
        var block = start;

        for (var step = 0; step < 4 && block is not null; step++)
        {
            if (block.Operations.Count > 0 || block.Condition is not null)
            {
                return block;
            }

            if (block.Next is null)
            {
                break;
            }

            block = block.Next;
        }

        return start;
    }

    /// <summary>
    /// Проверяет, достижим ли target из start.
    /// </summary>
    private bool ReachesFrom(ExprBlock? start, ExprBlock target)
    {
        if (start is null) return false;

        var seen = new HashSet<ExprBlock>();
        var queue = new Queue<ExprBlock>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var block = queue.Dequeue();
            if (!seen.Add(block)) continue;
            if (ReferenceEquals(block, target)) return true;
            if (block.Next != null) queue.Enqueue(block.Next);
            if (block.ConditionalBlock != null) queue.Enqueue(block.ConditionalBlock);
            if (seen.Count > 64) break;
        }
        return false;
    }

    /// <summary>
    /// Преобразует заголовок цикла в структурированную операцию с использованием анализатора.
    /// </summary>
    private void EmitLoopOperation(
        ExprBlock header,
        ExprBlock? merge,
        List<Operation> result,
        HashSet<ExprBlock> visited,
        ExprBlock? enclosingLoopExit,
        ExprBlock? enclosingLoopHeader)
    {
        if (header is null)
            return;

        // Отмечаем заголовок сразу
        visited.Add(header);

        // Сначала пробуем получить раскладку через старую логику
        var layout = TryGetLoopLayout(header);

        ExprBlock? bodyStart;
        ExprBlock? exitBlock;
        Expr continueCondition;

        if (layout != null)
        {
            bodyStart = layout.Value.BodyStart;
            exitBlock = layout.Value.ExitBlock;
            continueCondition = layout.Value.ContinueCondition;
        }
        else
        {
            // Fallback: определяем bodyStart и exitBlock вручную
            if (header.Next != null && !ReferenceEquals(header.Next, merge))
            {
                bodyStart = header.Next;
                exitBlock = header.ConditionalBlock;
                continueCondition = !header.Condition!;
            }
            else if (header.ConditionalBlock != null)
            {
                bodyStart = header.ConditionalBlock;
                exitBlock = header.Next;
                continueCondition = header.Condition!;
            }
            else
            {
                return;
            }
        }

        // Используем анализатор для определения типа цикла и параметров
        var loopResult = _loopAnalyzer.Analyze(header, _builder.Blocks, visited, enclosingLoopHeader);

        var loopType = loopResult?.LoopType ?? LoopType.While;
        var init = loopResult?.Init;
        var iteration = loopResult?.Iteration;

        // do-while: тело уже посещено до заголовка или заголовок имеет cond + uncond jmp
        var loopBody = new List<Operation>();
        var bodyVisited = new HashSet<ExprBlock>(visited);

        var isDoWhile = false;
        var instrs = header.BasicBlock.Instructions;
        if (instrs.Any(i => i.IsConditionalJump) && instrs.Any(i => i.IsUnconditionalJump))
        {
            isDoWhile = true;
            loopType = LoopType.DoWhile; // Принудительно устанавливаем для cond+uncond паттерна
        }

        // Проверка 1: тело уже посещено
        if (visited.Contains(bodyStart))
        {
            if (ReferenceEquals(bodyStart, header))
            {
                loopBody.AddRange(header.Operations);
                bodyVisited.Add(header);

                var removeCount = header.Operations.Count;
                if (removeCount > 0 && result.Count >= removeCount)
                {
                    result.RemoveRange(result.Count - removeCount, removeCount);
                }
            }
            else
            {
                var chain = CollectNextChain(bodyStart, header);
                foreach (var chainBlock in chain)
                {
                    loopBody.AddRange(chainBlock.Operations);
                    bodyVisited.Add(chainBlock);
                }

                RemoveLinearChainFromResult(result, chain);
            }
        }
        else if (isDoWhile)
        {
            // Для do-while: тело ещё не посещено, нужно собрать его из блоков перед заголовком
            // Ключевое: uncond jmp из заголовка ведёт на тело
            var headerInstrs = header.BasicBlock.Instructions;
            var uncondJmp = headerInstrs.FirstOrDefault(i => i.IsUnconditionalJump);

            ExprBlock? loopTarget = null;
            if (uncondJmp != null && uncondJmp.Operand1.Type is OperandType.Relative8 or OperandType.Relative16)
            {
                var jmpOffset = uncondJmp.Operand1.Value;
                var targetOffset = header.BasicBlock.StartOffset + 3 + (short)jmpOffset;
                loopTarget = _builder.Blocks.FirstOrDefault(b => b.BasicBlock.StartOffset == targetOffset);
            }

            var doWhileBody = CollectDoWhileBody(header, loopTarget ?? bodyStart);
            loopBody.AddRange(doWhileBody);
        }
        else
        {
            CollectOperations(
                bodyStart,
                loopBody,
                bodyVisited,
                stopBefore: header,
                enclosingLoopExit: exitBlock,
                enclosingLoopHeader: header);
        }

        if (isDoWhile)
            loopType = LoopType.DoWhile;

        foreach (var block in bodyVisited)
        {
            visited.Add(block);
        }

        SanitizeLoopBody(loopBody);

        // Создаём финальную операцию цикла
        Operation loopOp = loopType switch
        {
            LoopType.For => new ForOperation(init, continueCondition, iteration, loopBody),
            LoopType.DoWhile => new DoWhileOperation(continueCondition, loopBody),
            _ => new WhileOperation(continueCondition, loopBody)
        };

        result.Add(loopOp);

        StripLoopPreamble(result, header);
        CollectLoopExitOperations(exitBlock, merge, result, visited);
    }

    /// <summary>
    /// Собирает тело do-while цикла из блоков перед заголовком.
    /// </summary>
    private List<Operation> CollectDoWhileBody(ExprBlock header, ExprBlock? bodyStartHint)
    {
        var body = new List<Operation>();
        var bodyBlocks = new List<ExprBlock>();

        // Ключевое наблюдение: в do-while unconditional jmp из заголовка ведёт на тело
        var instrs = header.BasicBlock.Instructions;
        var uncondJmp = instrs.FirstOrDefault(i => i.IsUnconditionalJump);

        ExprBlock? loopTarget = null;
        if (uncondJmp != null && uncondJmp.Operand1.Type is OperandType.Relative8 or OperandType.Relative16)
        {
            // Вычисляем target offset для jmp
            var jmpOffset = uncondJmp.Operand1.Value;
            var targetOffset = header.BasicBlock.StartOffset + 3 + (short)jmpOffset;

            // Ищем блок с этим offset
            loopTarget = _builder.Blocks.FirstOrDefault(b => b.BasicBlock.StartOffset == targetOffset);
        }

        // Если не нашли по jmp, используем bodyStartHint
        var bodyEntry = loopTarget ?? bodyStartHint;

        if (bodyEntry != null)
        {
            // Собираем блоки от bodyEntry до header (не включая)
            var visited = new HashSet<ExprBlock>();
            var queue = new Queue<ExprBlock>();
            queue.Enqueue(bodyEntry);

            while (queue.Count > 0)
            {
                var block = queue.Dequeue();
                if (block == null || block == header || !visited.Add(block))
                    continue;

                bodyBlocks.Add(block);

                // Идём только вперёд по Next
                if (block.Next != null && block.Next != header && block.Next.BasicBlock.StartOffset < header.BasicBlock.StartOffset)
                {
                    queue.Enqueue(block.Next);
                }
            }

            // Сортируем блоки по offset
            bodyBlocks.Sort((a, b) => a.BasicBlock.StartOffset.CompareTo(b.BasicBlock.StartOffset));

            foreach (var block in bodyBlocks)
            {
                body.AddRange(block.Operations);
            }
        }
        else
        {
            // Fallback: ищем все блоки перед заголовком, которые имеют операции
            // И сортируем по offset
            var candidateBlocks = _builder.Blocks
                .Where(b => b.BasicBlock.StartOffset < header.BasicBlock.StartOffset && b.Operations.Count > 0)
                .OrderBy(b => b.BasicBlock.StartOffset)
                .ToList();

            foreach (var block in candidateBlocks)
            {
                body.AddRange(block.Operations);
            }
        }

        // Удаляем goto/label
        body.RemoveAll(op => op is LabelOperation or GotoOperation);

        return body;
    }

    /// <summary>
    /// Блоки тела по цепочке fallthrough до заголовка цикла.
    /// </summary>
    private static List<ExprBlock> CollectNextChain(ExprBlock? start, ExprBlock? stopBefore)
    {
        var chain = new List<ExprBlock>();
        var block = start;
        var seen = new HashSet<ExprBlock>();

        while (block is not null && block != stopBefore && seen.Add(block))
        {
            chain.Add(block);
            block = block.Next;
        }

        return chain;
    }

    /// <summary>
    /// Убирает из <paramref name="result"/> операции (и метки), уже перенесённые в тело цикла.
    /// </summary>
    private void RemoveLinearChainFromResult(List<Operation> result, IReadOnlyList<ExprBlock> chain)
    {
        if (chain.Count == 0)
        {
            return;
        }

        foreach (var chainBlock in chain)
        {
            var label = GetLabelForBlock(chainBlock);
            result.RemoveAll(op => op is LabelOperation l && l.Label == label);
        }

        var removeCount = chain.Sum(static b => b.Operations.Count);
        if (removeCount > 0 && result.Count >= removeCount)
        {
            result.RemoveRange(result.Count - removeCount, removeCount);
        }
    }

    /// <summary>
    /// Удаляет служебные goto/label внутри структурированного тела цикла.
    /// </summary>
    private static void SanitizeLoopBody(List<Operation> body)
    {
        body.RemoveAll(static op => op is LabelOperation or GotoOperation);
    }

    /// <summary>
    /// QuickC /Od: <c>init; goto header; label: for(...)</c> — убираем лишний goto и метку заголовка.
    /// </summary>
    private static void StripLoopPreamble(List<Operation> result, ExprBlock header)
    {
        var headerLabel = GetLabelForBlock(header);

        for (var i = result.Count - 1; i >= 0; i--)
        {
            if (result[i] is GotoOperation { Label: var target } && target == headerLabel)
            {
                result.RemoveAt(i);
                break;
            }
        }

        for (var i = result.Count - 1; i >= 0; i--)
        {
            if (result[i] is LabelOperation { Label: var name } && name == headerLabel)
            {
                result.RemoveAt(i);
                break;
            }
        }
    }

    /// <summary>
    /// Собирает код после выхода из цикла, не дублируя return из tail-эпилога.
    /// </summary>
    private void CollectLoopExitOperations(
        ExprBlock? exitStart,
        ExprBlock? merge,
        List<Operation> result,
        HashSet<ExprBlock> visited)
    {
        if (exitStart is null)
        {
            return;
        }

        var exitOps = new List<Operation>();
        var exitVisited = new HashSet<ExprBlock>(visited);
        var exitStop = ReferenceEquals(merge, exitStart) ? null : merge;

        var epilogueMerge = merge;
        if (epilogueMerge is null
            && exitStart.Next is not null
            && IsInlineEpilogueMerge(exitStart.Next))
        {
            epilogueMerge = exitStart.Next;
            exitStop = epilogueMerge;
        }

        CollectOperations(exitStart, exitOps, exitVisited, stopBefore: exitStop);

        if (epilogueMerge is not null && IsInlineEpilogueMerge(epilogueMerge))
        {
            AppendEpilogueReturnIfNeeded(exitStart, epilogueMerge, exitOps, exitVisited);
            visited.Add(epilogueMerge);
        }

        foreach (var block in exitVisited)
        {
            visited.Add(block);
        }

        result.AddRange(exitOps);
    }

    /// <summary>
    /// Проверяет, использует ли условие разыменование указателя (признак цикла по строке/массиву).
    /// </summary>
    public static bool ConditionUsesCharPointerDeref(Expr condition)
    {
        foreach (var mem in ExprSubstitution.CollectMemExprs(condition))
        {
            if (mem.Address is Variable or Math2Expr)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Условие на заголовке цикла: ветка «истина» снова достигает этот блок.</summary>
    protected virtual bool IsLoopHeader(ExprBlock block, ExprBlock? thenStart)
    {
        if (thenStart is null)
        {
            return false;
        }

        return CollectReachable(thenStart).Contains(block);
    }

    /// <summary>
    /// Точка слияния — только return (ранний выход), а не эпилог вроде <c>*dst = 0</c> после цикла.
    /// </summary>
    private bool MergeIsReturnOnly(ExprBlock merge, HashSet<ExprBlock> visited)
    {
        var probe = new List<Operation>();
        var probeVisited = new HashSet<ExprBlock>(visited);
        CollectOperations(merge, probe, probeVisited);
        return probe.Count > 0 && probe.All(static op => op is ReturnOperation);
    }

    private void AppendEpilogueReturnIfNeeded(
        ExprBlock? bodyStart,
        ExprBlock? merge,
        List<Operation> body,
        HashSet<ExprBlock> visited)
    {
        if (bodyStart is null || merge is null || body.Any(static op => op is ReturnOperation))
        {
            return;
        }

        if (!IsInlineEpilogueMerge(merge))
        {
            return;
        }

        var lastBlock = FindLastCollectedBlock(bodyStart, merge, visited);
        if (lastBlock is null)
        {
            return;
        }

        var returnValue = lastBlock.EndRegisters.Get16(GpRegister16.AX);
        body.Add(new ReturnOperation(returnValue, IsExplicit: true));
    }

    private static ExprBlock? FindLastCollectedBlock(ExprBlock? start, ExprBlock stopBefore, HashSet<ExprBlock> visited)
    {
        ExprBlock? last = null;
        var block = start;
        var localSeen = new HashSet<ExprBlock>();

        while (block is not null && block != stopBefore)
        {
            if (!visited.Contains(block) || !localSeen.Add(block))
            {
                break;
            }

            last = block;
            block = block.Next;
        }

        return last;
    }

    private static bool IsInlineEpilogueMerge(ExprBlock merge)
    {
        var instructions = merge.BasicBlock.Instructions;
        if (instructions.Count == 0 || !instructions[^1].IsReturn)
        {
            return false;
        }

        foreach (var instr in instructions)
        {
            if (instr.IsReturn)
            {
                continue;
            }

            if (instr.Mnemonic is not (Mnemonic.POP or Mnemonic.MOV or Mnemonic.LEAVE or Mnemonic.JMP))
            {
                return false;
            }
        }

        return true;
    }
}

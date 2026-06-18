namespace UltraDecompiler.Ir.Builder.Loops;

/// <summary>
/// Анализатор циклов для кода QuickC без оптимизации (/Od).
/// Распознаёт классические паттерны: for с переменной на стеке, while по указателю, do-while.
/// </summary>
public sealed class QuickCUnoptLoopAnalyzer : LoopAnalyzerBase
{
    /// <summary>
    /// Анализирует блок-заголовок цикла для кода /Od.
    /// </summary>
    public override LoopAnalysisResult? Analyze(
        ExprBlock headerBlock,
        IReadOnlyList<ExprBlock> allBlocks,
        HashSet<ExprBlock> visitedBlocks,
        ExprBlock? enclosingLoopHeader = null)
    {
        if (headerBlock.Condition is null)
            return null;

        // Определяем раскладку цикла
        var layout = GetLoopLayout(headerBlock, allBlocks);
        if (layout == null)
            return null;

        var (bodyStart, exitBlock, continueCondition) = layout.Value;

        // Определяем тип цикла (do-while если тело уже посещено или есть cond+uncond в инструкциях)
        var loopType = DetermineLoopType(headerBlock, bodyStart, visitedBlocks);

        // Для for-цикла пытаемся распознать счётчик
        Operation? init = null;
        Operation? iteration = null;

        if (TryGetLoopCounter(continueCondition, out var counter))
        {
            // Ищем инициализацию и итерацию
            init = FindInitForCounter(counter, headerBlock, allBlocks);
            iteration = FindIterationForCounter(counter, bodyStart, allBlocks, visitedBlocks);

            if (init != null && iteration != null)
            {
                loopType = LoopType.For;
            }
        }

        return new LoopAnalysisResult
        {
            LoopType = loopType,
            Init = init,
            Condition = continueCondition,
            Iteration = iteration,
            Body = [],
            ExitBlock = exitBlock
        };
    }

    /// <summary>
    /// Определяет, является ли блок заголовком цикла для /Od.
    /// </summary>
    public override bool IsLoopHeader(ExprBlock block, ExprBlock? enclosingLoopExit = null, ExprBlock? enclosingLoopHeader = null)
    {
        if (block.Condition is null)
            return false;

        var instrs = block.BasicBlock.Instructions;

        // do-while: заголовок имеет cond + uncond jmp назад
        if (instrs.Any(i => i.IsConditionalJump) && instrs.Any(i => i.IsUnconditionalJump))
        {
            var uncondJmp = instrs.Last(i => i.IsUnconditionalJump);
            if (uncondJmp.Operand1.Type is OperandType.Relative8 or OperandType.Relative16)
            {
                var targetOffset = block.BasicBlock.StartOffset + 3 + (short)uncondJmp.Operand1.Value;
                if (targetOffset < block.BasicBlock.StartOffset)
                {
                    // uncond jmp ведёт назад - это do-while заголовок
                    return true;
                }
            }

            if (BlockHasBackEdgeToSelf(block))
                return true;
        }

        // Для /Od: заголовок цикла имеет back-edge или условный переход назад
        if (BlockHasBackEdgeToSelf(block))
            return true;

        if (HasRawBackwardJump(block))
            return true;

        // Проверяем, ведёт ли ветка обратно к заголовку
        if (block.ConditionalBlock != null && ReachesLoopHeader(block.ConditionalBlock, block))
            return true;

        if (block.Next != null && ReachesLoopHeader(block.Next, block))
            return true;

        // Fallback: если есть условие и блок выглядит как тест (cmp + jcc)
        if (instrs.Count >= 2)
        {
            var lastTwo = instrs.TakeLast(2).ToList();
            if (lastTwo.Any(i => i.Mnemonic == Mnemonic.CMP) && lastTwo.Any(i => i.IsConditionalJump))
            {
                // Проверяем, что conditional branch ведёт назад (к телу), а не вперёд (на break)
                var condJmp = instrs.Last(i => i.IsConditionalJump);
                if (condJmp.Operand1.Type is OperandType.Relative8 or OperandType.Relative16)
                {
                    var condTargetOffset = block.BasicBlock.StartOffset + 3 + (short)condJmp.Operand1.Value;
                    if (condTargetOffset < block.BasicBlock.StartOffset)
                    {
                        // conditional branch ведёт назад - это заголовок цикла
                        return true;
                    }
                }

                // Если есть uncond jmp, проверяем его направление
                if (instrs.Any(i => i.IsUnconditionalJump))
                {
                    var uncondJmp = instrs.Last(i => i.IsUnconditionalJump);
                    if (uncondJmp.Operand1.Type is OperandType.Relative8 or OperandType.Relative16)
                    {
                        var uncondTargetOffset = block.BasicBlock.StartOffset + 3 + (short)uncondJmp.Operand1.Value;
                        if (uncondTargetOffset < block.BasicBlock.StartOffset)
                        {
                            return true; // uncond jmp ведёт назад
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Проверяет, ведёт ли блок обратно к loopHeader через back-edge.
    /// </summary>
    private new bool ReachesLoopHeader(ExprBlock? start, ExprBlock loopHeader, int maxSteps = 8)
    {
        if (start is null)
            return false;

        var visited = new HashSet<ExprBlock>();
        var queue = new Queue<ExprBlock>();
        queue.Enqueue(start);

        while (queue.Count > 0 && visited.Count < maxSteps)
        {
            var block = queue.Dequeue();
            if (!visited.Add(block))
                continue;

            if (ReferenceEquals(block, loopHeader))
                return true;

            if (block.BasicBlock.Instructions.Any(i => i.IsUnconditionalJump))
            {
                var jmp = block.BasicBlock.Instructions.Last(i => i.IsUnconditionalJump);
                if (jmp.Operand1.Type is OperandType.Relative8 or OperandType.Relative16)
                {
                    var targetOffset = block.BasicBlock.StartOffset + 3 + (short)jmp.Operand1.Value;
                    if (targetOffset == loopHeader.BasicBlock.StartOffset)
                        return true;
                }
            }

            if (block.Next != null)
                queue.Enqueue(block.Next);
            if (block.ConditionalBlock != null)
                queue.Enqueue(block.ConditionalBlock);
        }

        return false;
    }

    /// <summary>
    /// Определяет раскладку цикла: тело, выход, условие продолжения.
    /// </summary>
    private LoopLayout? GetLoopLayout(ExprBlock headerBlock, IReadOnlyList<ExprBlock> allBlocks)
    {
        if (headerBlock.Condition is null)
            return null;

        // Проверка на do-while: заголовок имеет cond + uncond jmp
        var instrs = headerBlock.BasicBlock.Instructions;
        bool isDoWhile = instrs.Any(i => i.IsConditionalJump) && instrs.Any(i => i.IsUnconditionalJump);

        if (isDoWhile)
        {
            // Для do-while: тело - это блоки ПЕРЕД заголовком по offset
            var bodyBlocks = allBlocks
                .Where(b => b.BasicBlock.StartOffset < headerBlock.BasicBlock.StartOffset)
                .Where(b => b.Operations.Count > 0)
                .OrderByDescending(b => b.BasicBlock.StartOffset)
                .ToList();

            if (bodyBlocks.Count > 0)
            {
                // Тело начинается с самого дальнего блока перед заголовком
                var bodyStart = bodyBlocks.Last(); // Первый по порядку
                var exitBlock = headerBlock.Next; // fallthrough после условного перехода
                return new LoopLayout(bodyStart, exitBlock, !headerBlock.Condition);
            }
        }

        // Паттерн 1: fallthrough (Next) ведёт в тело
        if (headerBlock.Next is not null)
        {
            var bodyEntry = ResolveLoopBodyStart(headerBlock.Next);
            if (bodyEntry != null && ReachesFrom(bodyEntry, headerBlock, allBlocks))
            {
                return new LoopLayout(bodyEntry, headerBlock.ConditionalBlock, !headerBlock.Condition);
            }
        }

        // Паттерн 2: conditional ветка ведёт в тело
        if (headerBlock.ConditionalBlock is not null)
        {
            var bodyEntry = ResolveLoopBodyStart(headerBlock.ConditionalBlock);
            if (bodyEntry != null && ReachesFrom(bodyEntry, headerBlock, allBlocks))
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

        // Fallback для back-edge
        if (HasRawBackwardJump(headerBlock) || BlockHasBackEdgeToSelf(headerBlock))
        {
            ExprBlock? body = null;
            if (headerBlock.Next != null)
                body = ResolveLoopBodyStart(headerBlock.Next);
            if (body == null && headerBlock.ConditionalBlock != null)
                body = ResolveLoopBodyStart(headerBlock.ConditionalBlock);
            if (body == null)
                body = headerBlock;

            var exit = headerBlock.Next != null ? headerBlock.ConditionalBlock : headerBlock.Next;
            return new LoopLayout(body, exit, headerBlock.Condition);
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
    private static bool ReachesFrom(ExprBlock? start, ExprBlock target, IReadOnlyList<ExprBlock> allBlocks)
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
    /// Определяет тип цикла по структуре.
    /// </summary>
    private static LoopType DetermineLoopType(ExprBlock headerBlock, ExprBlock bodyStart, HashSet<ExprBlock> visitedBlocks)
    {
        if (visitedBlocks.Contains(bodyStart) && !ReferenceEquals(bodyStart, headerBlock))
        {
            return LoopType.DoWhile;
        }

        if (IsDoWhileByInstructions(headerBlock))
        {
            return LoopType.DoWhile;
        }

        return LoopType.While;
    }

    /// <summary>
    /// Ищет инициализацию счётчика перед заголовком цикла.
    /// </summary>
    private static SetOperation? FindInitForCounter(Variable counter, ExprBlock headerBlock, IReadOnlyList<ExprBlock> allBlocks)
    {
        foreach (var block in allBlocks)
        {
            if (block == headerBlock)
                break;

            foreach (var op in block.Operations)
            {
                if (op is SetOperation { Dst: Variable dst } set && SameVariable(dst, counter))
                    return set;
            }
        }

        return null;
    }

    /// <summary>
    /// Ищет обновление счётчика в теле цикла.
    /// </summary>
    private static Operation? FindIterationForCounter(Variable counter, ExprBlock bodyStart, IReadOnlyList<ExprBlock> allBlocks, HashSet<ExprBlock> visited)
    {
        var bodyBlocks = CollectReachable(bodyStart);
        var sortedBlocks = bodyBlocks.OrderBy(b => b.BasicBlock.StartOffset).ToList();

        for (int i = sortedBlocks.Count - 1; i >= 0; i--)
        {
            var block = sortedBlocks[i];
            foreach (var op in block.Operations)
            {
                if (IsIterationOperation(op, counter))
                    return op;
            }
        }

        return null;
    }
}


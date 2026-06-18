namespace UltraDecompiler.Ir.Builder.Loops;

/// <summary>
/// Анализатор циклов для кода QuickC с оптимизацией (/Ot, /Ox).
/// Распознаёт циклы со счётчиком в регистрах (SI, DI, BX, CX),
/// паттерны с and reg,reg + jg, inc/dec вместо add/sub.
/// </summary>
public sealed class QuickCOptLoopAnalyzer : LoopAnalyzerBase
{
    /// <summary>
    /// Анализирует блок-заголовок цикла для кода /Ox.
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

        // Определяем тип цикла
        var loopType = DetermineLoopType(headerBlock, bodyStart, visitedBlocks);

        // Для for-цикла пытаемся распознать счётчик в регистре
        Operation? init = null;
        Operation? iteration = null;

        if (TryGetLoopCounter(continueCondition, out var counter))
        {
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
    /// Определяет, является ли блок заголовком цикла для /Ox.
    /// </summary>
    public override bool IsLoopHeader(ExprBlock block, ExprBlock? enclosingLoopExit = null, ExprBlock? enclosingLoopHeader = null)
    {
        if (block.Condition is null)
        {
            return false;
        }

        // Для /Ox: проверяем типичные паттерны заголовков регистровых циклов
        if (HasOxLoopHeaderPattern(block))
            return true;

        if (BlockHasBackEdgeToSelf(block))
            return true;

        if (HasRawBackwardJump(block))
            return true;

        return false;
    }

    /// <summary>
    /// Проверяет типичный паттерн заголовка цикла /Ox: CMP reg,imm + Jl/Jg или AND reg,reg + Jg.
    /// </summary>
    private static bool HasOxLoopHeaderPattern(ExprBlock block)
    {
        var instrs = block.BasicBlock.Instructions;

        for (int i = 0; i < instrs.Count - 1; i++)
        {
            var a = instrs[i];
            var b = instrs[i + 1];

            // Паттерн: CMP reg,imm + Jl/Jb/Jle/Jbe
            if (a.Mnemonic == Mnemonic.CMP
                && a.Operand1.Type == OperandType.Register16
                && a.Operand2.Type == OperandType.Immediate16
                && (b.Mnemonic is Mnemonic.JL or Mnemonic.JB or Mnemonic.JLE or Mnemonic.JBE))
            {
                return true;
            }

            // Паттерн: AND reg,reg + Jg/Jge (тест на > 0 после dec)
            if (a.Mnemonic == Mnemonic.AND
                && a.Operand1.Type == OperandType.Register16
                && a.Operand2.Type == OperandType.Register16
                && (b.Mnemonic is Mnemonic.JG or Mnemonic.JGE))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Определяет раскладку цикла для /Ox.
    /// </summary>
    private static LoopLayout? GetLoopLayout(ExprBlock headerBlock, IReadOnlyList<ExprBlock> allBlocks)
    {
        if (headerBlock.Condition is null)
            return null;

        // Для /Ox: часто forward jmp to test, тело идёт по fallthrough
        if (headerBlock.Next is not null)
        {
            var bodyEntry = ResolveLoopBodyStart(headerBlock.Next);
            if (bodyEntry != null && ReachesFrom(bodyEntry, headerBlock, allBlocks))
            {
                return new LoopLayout(bodyEntry, headerBlock.ConditionalBlock, !headerBlock.Condition);
            }
        }

        // Паттерн: conditional ветка ведёт в тело
        if (headerBlock.ConditionalBlock is not null)
        {
            var bodyEntry = ResolveLoopBodyStart(headerBlock.ConditionalBlock);
            if (bodyEntry != null && ReachesFrom(bodyEntry, headerBlock, allBlocks))
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
    /// Пропускает пустые jmp-блоки до входа в тело.
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
    /// Определяет тип цикла.
    /// </summary>
    private static LoopType DetermineLoopType(ExprBlock headerBlock, ExprBlock bodyStart, HashSet<ExprBlock> visitedBlocks)
    {
        // do-while: тело уже посещено или блок имеет cond + uncond jmp
        if (IsDoWhileByInstructions(headerBlock))
        {
            return LoopType.DoWhile;
        }

        return LoopType.While;
    }

    /// <summary>
    /// Извлекает инициализацию из тела (для /Ox: mov reg, imm в начале).
    /// </summary>
    private static SetOperation? FindInitForCounter(Variable counter, ExprBlock headerBlock, IReadOnlyList<ExprBlock> allBlocks)
    {
        for (var i = 0; i < allBlocks.Count; i++)
        {
            if (allBlocks[i] == headerBlock)
                break;

            foreach (var op in allBlocks[i].Operations)
            {
                if (op is SetOperation { Dst: Variable dst } set && SameVariable(dst, counter))
                    return set;
            }
        }

        return null;
    }

    /// <summary>
    /// Извлекает обновление счётчика для /Ox (inc/dec/reg-арифметика).
    /// </summary>
    private static Operation? FindIterationForCounter(Variable counter, ExprBlock bodyStart, IReadOnlyList<ExprBlock> allBlocks, HashSet<ExprBlock> visited)
    {
        var bodyBlocks = CollectBodyBlocks(bodyStart, visited);

        foreach (var block in bodyBlocks)
        {
            foreach (var op in block.Operations)
            {
                if (IsIterationOperation(op, counter))
                    return op;
            }
        }

        return null;
    }

    /// <summary>
    /// Собирает блоки тела цикла.
    /// </summary>
    private static List<ExprBlock> CollectBodyBlocks(ExprBlock bodyStart, HashSet<ExprBlock> visited, int maxBlocks = 16)
    {
        var blocks = new List<ExprBlock>();
        var block = bodyStart;
        var steps = 0;

        while (block != null && steps++ < maxBlocks)
        {
            if (!visited.Add(block))
                break;

            blocks.Add(block);

            if (block.Condition != null)
                break;

            block = block.Next;
        }

        return blocks;
    }
}

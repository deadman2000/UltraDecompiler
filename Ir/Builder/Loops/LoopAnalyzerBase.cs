namespace UltraDecompiler.Ir.Builder.Loops;

/// <summary>
/// Базовый класс для анализаторов циклов QuickC.
/// Содержит общую логику распознавания структуры цикла.
/// </summary>
public abstract class LoopAnalyzerBase : ILoopAnalyzer
{
    /// <summary>
    /// Анализирует блок-заголовок цикла и определяет его тип и параметры.
    /// </summary>
    public abstract LoopAnalysisResult? Analyze(
        ExprBlock headerBlock,
        IReadOnlyList<ExprBlock> allBlocks,
        HashSet<ExprBlock> visitedBlocks,
        ExprBlock? enclosingLoopHeader = null);

    /// <summary>
    /// Определяет, является ли блок заголовком цикла согласно профилю компиляции.
    /// </summary>
    public abstract bool IsLoopHeader(ExprBlock block, ExprBlock? enclosingLoopExit = null, ExprBlock? enclosingLoopHeader = null);

    /// <summary>
    /// Находит тело цикла, начиная с блока входа в тело.
    /// </summary>
    protected static List<Operation> CollectLoopBody(
        ExprBlock bodyStart,
        ExprBlock headerBlock,
        HashSet<ExprBlock> visitedBlocks,
        ExprBlock? exitBlock = null)
    {
        var body = new List<Operation>();
        var bodyVisited = new HashSet<ExprBlock>();

        CollectOperationsForBody(bodyStart, body, bodyVisited, headerBlock, exitBlock);

        // Помечаем блоки тела как посещённые
        foreach (var block in bodyVisited)
        {
            visitedBlocks.Add(block);
        }

        // Удаляем служебные goto/label из тела цикла
        SanitizeLoopBody(body);

        return body;
    }

    /// <summary>
    /// Собирает операции тела цикла, останавливаясь перед заголовком или выходом.
    /// </summary>
    private static void CollectOperationsForBody(
        ExprBlock? block,
        List<Operation> body,
        HashSet<ExprBlock> visited,
        ExprBlock stopBefore,
        ExprBlock? exitBlock)
    {
        var queue = new Queue<ExprBlock>();
        if (block != null && block != stopBefore)
        {
            queue.Enqueue(block);
        }

        var processed = new HashSet<ExprBlock>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!processed.Add(current) || current == stopBefore)
            {
                continue;
            }

            visited.Add(current);
            body.AddRange(current.Operations);

            // Не идём по ветке выхода
            if (ReferenceEquals(current.ConditionalBlock, exitBlock) || ReferenceEquals(current.Next, exitBlock))
            {
                continue;
            }

            if (current.Next != null && current.Next != stopBefore && !processed.Contains(current.Next))
            {
                queue.Enqueue(current.Next);
            }

            if (current.ConditionalBlock != null && current.ConditionalBlock != stopBefore && !processed.Contains(current.ConditionalBlock))
            {
                queue.Enqueue(current.ConditionalBlock);
            }
        }
    }

    /// <summary>
    /// Удаляет служебные goto и label из тела цикла.
    /// </summary>
    protected static void SanitizeLoopBody(List<Operation> body)
    {
        body.RemoveAll(op => op is LabelOperation or GotoOperation);
    }

    /// <summary>
    /// Определяет, является ли условие сравнением на равенство (для break/continue).
    /// </summary>
    protected static bool IsEqualityTest(Expr condition) =>
        condition is CmpExpr { Operation: CmpOperation.Eq or CmpOperation.Ne };

    /// <summary>
    /// Извлекает переменную-счётчик из условия цикла.
    /// </summary>
    protected static bool TryGetLoopCounter(Expr condition, out Variable counter)
    {
        counter = null!;

        if (condition is CmpExpr cmp)
        {
            if (cmp.Left is Variable left && !left.IsTemp)
            {
                counter = left;
                return true;
            }

            if (cmp.Right is Variable right && !right.IsTemp)
            {
                counter = right;
                return true;
            }
        }

        if (condition is Math1Expr { Operation: Math1Operation.Not, Op: CmpExpr inner })
        {
            if (inner.Left is Variable left2 && !left2.IsTemp)
            {
                counter = left2;
                return true;
            }

            if (inner.Right is Variable right2 && !right2.IsTemp)
            {
                counter = right2;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Проверяет, является ли операция обновлением счётчика (inc/dec/add/sub).
    /// </summary>
    protected static bool IsIterationOperation(Operation op, Variable counter)
    {
        return op switch
        {
            IncOperation inc => ExprReferencesVariable(inc.Target, counter),
            DecOperation dec => ExprReferencesVariable(dec.Target, counter),
            AddAssignOperation add => ExprReferencesVariable(add.Target, counter),
            SubAssignOperation sub => ExprReferencesVariable(sub.Target, counter),
            SetOperation { Dst: Variable dst, Src: Math2Expr math }
                when ExprReferencesVariable(dst, counter)
                     && ExprReferencesVariable(math.First, counter)
                     && math.Second is ConstExpr { Value: not 0 }
                     && math.Operation is Math2Operation.Add or Math2Operation.Sub or Math2Operation.Mul => true,
            _ => false
        };
    }

    /// <summary>
    /// Извлекает операцию инициализации счётчика из списка операций перед циклом.
    /// </summary>
    protected static bool TryExtractInit(List<Operation> operations, Variable counter, out SetOperation init)
    {
        for (var i = operations.Count - 1; i >= 0; i--)
        {
            if (operations[i] is SetOperation { Dst: Variable dst } set && SameVariable(dst, counter))
            {
                init = set;
                operations.RemoveAt(i);
                return true;
            }
        }

        init = null!;
        return false;
    }

    /// <summary>
    /// Извлекает операцию обновления счётчика из конца тела цикла.
    /// </summary>
    protected static bool TryExtractIteration(List<Operation> body, Variable counter, out Operation iteration, out List<Operation> bodyWithoutIteration)
    {
        iteration = null!;
        bodyWithoutIteration = new List<Operation>(body);

        if (body.Count == 0)
        {
            return false;
        }

        var last = body[^1];
        if (IsIterationOperation(last, counter))
        {
            iteration = last;
            bodyWithoutIteration.RemoveAt(bodyWithoutIteration.Count - 1);
            return true;
        }

        // Проверка паттерна через temp: add [bp-N], K → temp = var + K; var = temp
        if (body.Count >= 2 && TryMatchCounterStepViaTemp(body[^2], last, counter, out iteration))
        {
            bodyWithoutIteration.RemoveRange(bodyWithoutIteration.Count - 2, 2);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Распознаёт шаг счётчика через временную переменную.
    /// </summary>
    private static bool TryMatchCounterStepViaTemp(Operation prev, Operation last, Variable counter, out Operation iteration)
    {
        iteration = null!;

        if (last is not SetOperation { Dst: Variable dst, Src: Variable temp })
        {
            return false;
        }

        if (!SameVariable(dst, counter))
        {
            return false;
        }

        if (prev is not SetOperation { Dst: Variable tempDst, Src: Math2Expr math })
        {
            return false;
        }

        if (!ReferenceEquals(tempDst, temp))
        {
            return false;
        }

        if (math.Second is not ConstExpr { Value: not 0 })
        {
            return false;
        }

        if (!ExprReferencesVariable(math.First, counter))
        {
            return false;
        }

        iteration = new SetOperation(dst, math);
        return true;
    }

    /// <summary>
    /// Проверяет, ссылается ли выражение на указанную переменную.
    /// </summary>
    protected static bool ExprReferencesVariable(Expr expr, Variable counter)
    {
        return expr switch
        {
            Variable v => SameVariable(v, counter),
            Math1Expr m1 => ExprReferencesVariable(m1.Op, counter),
            Math2Expr m2 => ExprReferencesVariable(m2.First, counter) || ExprReferencesVariable(m2.Second, counter),
            MemExpr mem => ExprReferencesVariable(mem.Address, counter),
            _ => false
        };
    }

    /// <summary>
    /// Сравнивает две переменные (по ссылке или по стековому слоту).
    /// </summary>
    protected static bool SameVariable(Variable left, Variable right) =>
        ReferenceEquals(left, right)
        || (left.IsStack && right.IsStack && left.Number == right.Number);

    /// <summary>
    /// Собирает все блоки, достижимые из начального блока.
    /// </summary>
    protected static HashSet<ExprBlock> CollectReachable(ExprBlock? start)
    {
        var result = new HashSet<ExprBlock>();
        if (start == null)
            return result;

        var queue = new Queue<ExprBlock>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var block = queue.Dequeue();
            if (!result.Add(block))
                continue;

            if (block.Next != null)
                queue.Enqueue(block.Next);

            if (block.ConditionalBlock != null)
                queue.Enqueue(block.ConditionalBlock);
        }

        return result;
    }

    /// <summary>
    /// Проверяет, ведёт ли ветка к заголовку цикла (для continue).
    /// </summary>
    protected static bool ReachesLoopHeader(ExprBlock? start, ExprBlock loopHeader, int maxSteps = 4)
    {
        if (start is null)
            return false;

        var visited = new HashSet<ExprBlock>();
        var block = start;

        for (var step = 0; step < maxSteps && block is not null; step++)
        {
            if (ReferenceEquals(block, loopHeader))
                return true;

            if (!visited.Add(block))
                return false;

            if (block.Operations.Count == 0 && ReferenceEquals(block.Next, loopHeader))
                return true;

            var instructions = block.BasicBlock.Instructions;
            if (instructions.Count == 1 && instructions[0].IsUnconditionalJump && ReferenceEquals(block.Next, loopHeader))
                return true;

            // Если блок содержит операции тела — это не continue-ветка
            if (block.Operations.Count > 0 && !IsIterationStepBlock(block))
                return false;

            block = block.Next;
        }

        return false;
    }

    /// <summary>
    /// Проверяет, является ли блок шагом итерации.
    /// </summary>
    private static bool IsIterationStepBlock(ExprBlock block) =>
        block.Operations.Count > 0 && block.Operations.All(IsIterationStepOperation);

    /// <summary>
    /// Проверяет, является ли операция шагом итерации.
    /// </summary>
    private static bool IsIterationStepOperation(Operation op) =>
        op switch
        {
            IncOperation or DecOperation => true,
            AddAssignOperation { Value: ConstExpr } => true,
            SubAssignOperation { Value: ConstExpr } => true,
            SetOperation { Dst: Variable dst, Src: Math2Expr { First: Variable first, Second: ConstExpr } math }
                when ReferenceEquals(dst, first)
                && math.Operation is Math2Operation.Add or Math2Operation.Sub or Math2Operation.Mul => true,
            _ => false
        };

    /// <summary>
    /// Проверяет, ведёт ли ветка к выходу из цикла (для break).
    /// </summary>
    protected static bool ReachesExitBlock(ExprBlock? start, ExprBlock exitBlock, int maxSteps = 4)
    {
        if (start is null)
            return false;

        var visited = new HashSet<ExprBlock>();
        var block = start;

        for (var step = 0; step < maxSteps && block is not null; step++)
        {
            if (ReferenceEquals(block, exitBlock))
                return true;

            if (!visited.Add(block))
                return false;

            if (block.Operations.Count == 0 && ReferenceEquals(block.Next, exitBlock))
                return true;

            var instructions = block.BasicBlock.Instructions;
            if (instructions.Count == 1 && instructions[0].IsUnconditionalJump && ReferenceEquals(block.Next, exitBlock))
                return true;

            block = block.Next ?? block.ConditionalBlock;
        }

        return false;
    }

    /// <summary>
    /// Определяет, является ли цикл do-while по инструкциям в заголовке.
    /// </summary>
    protected static bool IsDoWhileByInstructions(ExprBlock headerBlock)
    {
        var instructions = headerBlock.BasicBlock.Instructions;

        // do-while в QuickC: заголовок содержит условный + безусловный переход назад
        if (instructions.Any(i => i.IsConditionalJump) && instructions.Any(i => i.IsUnconditionalJump))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Проверяет наличие обратного прыжка в RAW-инструкциях.
    /// </summary>
    protected static bool HasRawBackwardJump(ExprBlock block)
    {
        int current = block.BasicBlock.StartOffset;
        bool hasBackward = false;

        if (block.ConditionalBlock != null && block.ConditionalBlock.BasicBlock.StartOffset < current)
            hasBackward = true;
        if (block.Next != null && block.Next.BasicBlock.StartOffset < current)
            hasBackward = true;

        if (!hasBackward)
            return false;

        // Проверяем, что блок содержит тест условия (CMP/TEST/AND)
        var instrs = block.BasicBlock.Instructions;
        return instrs.Any(i => i.Mnemonic is Mnemonic.CMP or Mnemonic.TEST or Mnemonic.AND);
    }

    /// <summary>
    /// Проверяет наличие back-edge к самому себе.
    /// </summary>
    protected static bool BlockHasBackEdgeToSelf(ExprBlock block)
    {
        if (block.Condition is null)
            return false;

        var reach = CollectReachable(block.Next ?? block.ConditionalBlock);
        foreach (var r in reach)
        {
            if (ReferenceEquals(r, block) || ReferenceEquals(r.Next, block) || ReferenceEquals(r.ConditionalBlock, block))
                return true;
        }

        return false;
    }
}

namespace UltraDecompiler.Ir.Builder;

public partial class ExpressionBuilder
{
    /// <summary>
    /// Возвращает линейный список операций всей декомпилированной программы.
    ///
    /// Обходит дерево <see cref="ExprBlock"/> от точки входа, разворачивая связи
    /// <see cref="ExprBlock.ConditionalBlock"/> / <see cref="ExprBlock.Next"/> в
    /// <see cref="IfOperation"/> (ветка «истина» — переход по условию, «ложь» — fallthrough).
    /// Операции управления потоком, уже вложенные в <see cref="ExprBlock.Operations"/>
    /// (<see cref="WhileOperation"/>, <see cref="ForOperation"/>), сохраняются как есть.
    /// </summary>
    public IReadOnlyList<Operation> GetAllOperations()
    {
        if (_entryBlock == null)
            return [];

        var result = new List<Operation>();
        var visited = new HashSet<ExprBlock>();
        CollectOperations(_entryBlock, result, visited);
        return result;
    }

    /// <summary>
    /// Рекурсивно перечисляет все операции, включая тела <see cref="IfOperation"/>,
    /// <see cref="WhileOperation"/> и <see cref="ForOperation"/>.
    /// </summary>
    public static IEnumerable<Operation> EnumerateNested(IEnumerable<Operation> operations)
    {
        foreach (var op in operations)
        {
            yield return op;

            switch (op)
            {
                case IfOperation i:
                    foreach (var nested in EnumerateNested(i.ThenBody))
                        yield return nested;
                    if (i.ElseBody != null)
                    {
                        foreach (var nested in EnumerateNested(i.ElseBody))
                            yield return nested;
                    }
                    break;
                case WhileOperation w:
                    foreach (var nested in EnumerateNested(w.Body))
                        yield return nested;
                    break;
                case ForOperation f:
                    if (f.Init != null)
                        yield return f.Init;
                    foreach (var nested in EnumerateNested(f.Body))
                        yield return nested;
                    if (f.Iteration != null)
                        yield return f.Iteration;
                    break;
                case SwitchOperation s:
                    foreach (var switchCase in s.Cases)
                    {
                        foreach (var nested in EnumerateNested(switchCase.Body))
                            yield return nested;
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Рекурсивно собирает операции, начиная с <paramref name="block"/>, до <paramref name="stopBefore"/>
    /// или до повторного посещения блока (точка слияния / обратное ребро цикла).
    /// </summary>
    private void CollectOperations(
        ExprBlock? block,
        List<Operation> result,
        HashSet<ExprBlock> visited,
        ExprBlock? stopBefore = null)
    {
        while (block != null && block != stopBefore)
        {
            if (!visited.Add(block))
                return;

            if (_switchByEntry.TryGetValue(block.BasicBlock.StartOffset, out var switchPattern))
            {
                CollectQuickCSwitch(block, switchPattern, result, visited);
                if (!_blocksByOffset.TryGetValue(switchPattern.MergeOffset, out block))
                {
                    return;
                }

                continue;
            }

            result.AddRange(block.Operations);
            if (EndsWithReturn(block.Operations))
            {
                return;
            }

            if (block.ConditionalBlock != null && block.Condition != null)
            {
                var merge = FindMerge(block.ConditionalBlock, block.Next);

                if (block.Next is not null && ShouldConvertLoopHeader(block))
                {
                    var loopBody = new List<Operation>();
                    CollectOperations(block.Next, loopBody, visited, stopBefore: block);
                    result.Add(new WhileOperation(InvertCondition(block.Condition), loopBody));

                    if (block.ConditionalBlock is not null)
                    {
                        var exitOps = new List<Operation>();
                        CollectOperations(block.ConditionalBlock, exitOps, visited, stopBefore: merge);
                        result.AddRange(exitOps);
                    }

                    if (merge is not null && !visited.Contains(merge))
                    {
                        block = merge;
                        continue;
                    }

                    return;
                }

                var thenStop = ReferenceEquals(merge, block.ConditionalBlock) ? null : merge;

                var thenBody = new List<Operation>();
                CollectOperations(block.ConditionalBlock, thenBody, visited, stopBefore: thenStop);
                if (!ShouldConvertLoopHeader(block))
                {
                    AppendEpilogueReturnIfNeeded(block.ConditionalBlock, merge, thenBody, visited);
                }

                List<Operation>? elseBody = null;
                var mergeUsedAsElse = false;
                if (block.Next != null)
                {
                    var elseOps = new List<Operation>();
                    CollectOperations(block.Next, elseOps, visited, stopBefore: merge);
                    if (elseOps.Count > 0)
                    {
                        elseBody = elseOps;
                    }
                    else if (merge is not null
                             && !ReferenceEquals(merge, block.ConditionalBlock)
                             && !IsLoopHeader(block, block.ConditionalBlock)
                             && MergeIsReturnOnly(merge, visited))
                    {
                        elseBody = [];
                        CollectOperations(merge, elseBody, visited);
                        visited.Add(merge);
                        mergeUsedAsElse = true;
                    }
                }

                result.Add(new IfOperation(block.Condition, thenBody, elseBody));

                if (BranchEndsWithReturn(thenBody) && (elseBody is null || BranchEndsWithReturn(elseBody)))
                {
                    if (merge is not null)
                    {
                        visited.Add(merge);
                    }

                    return;
                }

                if (merge is not null && !mergeUsedAsElse && !visited.Contains(merge))
                {
                    block = merge;
                    continue;
                }

                return;
            }

            block = block.Next;
        }
    }

    /// <summary>
    /// Находит ближайшую общую точку слияния двух веток (первый общий блок по смещению).
    /// </summary>
    private static ExprBlock? FindMerge(ExprBlock? thenStart, ExprBlock? elseStart)
    {
        if (thenStart == null || elseStart == null)
            return null;

        var thenReach = CollectReachable(thenStart);
        var elseReach = CollectReachable(elseStart);

        ExprBlock? merge = null;
        int mergeOffset = int.MaxValue;

        foreach (var candidate in thenReach)
        {
            if (ReferenceEquals(candidate, thenStart))
            {
                continue;
            }

            if (!elseReach.Contains(candidate))
            {
                continue;
            }

            int offset = candidate.BasicBlock.StartOffset;
            if (offset < mergeOffset)
            {
                mergeOffset = offset;
                merge = candidate;
            }
        }

        return merge;
    }

    /// <summary>
    /// Все блоки, достижимые по <see cref="ExprBlock.Next"/> и <see cref="ExprBlock.ConditionalBlock"/>.
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

    private static bool EndsWithReturn(IReadOnlyList<Operation> operations) =>
        operations.Count > 0 && operations[^1] is ReturnOperation;

    private static bool BranchEndsWithReturn(IReadOnlyList<Operation> body) =>
        body.Any(static op => op is ReturnOperation);

    /// <summary>
    /// Определяет, следует ли конвертировать заголовок цикла в WhileOperation.
    /// Переопределяется в профилях для разной эвристики (/Od vs /Ox).
    /// </summary>
    protected virtual bool ShouldConvertLoopHeader(ExprBlock block)
    {
        if (block.Next is null || block.Condition is null)
        {
            return false;
        }

        var isLoopHeader = IsLoopHeader(block, block.Next);
        if (!isLoopHeader)
        {
            return false;
        }

        if (IsArgcBoundLoopHeader(block.Condition))
        {
            return false;
        }

        // Не конвертируем if, условие которого — простая временная переменная
        // (это обработка флагов внутри цикла, а не заголовок цикла)
        if (IsTempVariableCondition(block.Condition))
        {
            return false;
        }

        return ConditionUsesCharPointerDeref(block.Condition)
            || LoopBodyAdvancesPointer(block.Next);
    }

    /// <summary>
    /// Проверяет, является ли условие сравнением временной переменной с константой
    /// (например, temp5 == 0 или temp5 != 0). Такие условия обычно обрабатывают флаги
    /// внутри цикла, а не являются заголовком цикла.
    /// </summary>
    protected static bool IsTempVariableCondition(Expr condition)
    {
        // Проверяем, что условие — это сравнение временной переменной с константой
        // вида temp5 == 0 или temp5 != 0
        if (condition is not CmpExpr cmp)
        {
            return false;
        }

        // Проверяем левую часть
        if (cmp.Left is Variable var && var.IsTemp)
        {
            return true;
        }

        // Проверяем правую часть
        if (cmp.Right is Variable var2 && var2.IsTemp)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Проверяет, является ли условие циклом по argc (такие циклы не конвертируем).
    /// </summary>
    protected static bool IsArgcBoundLoopHeader(Expr condition) =>
        condition is CmpExpr { Operation: CmpOperation.Uge or CmpOperation.Ugt, Right: Variable { Name: "argc" } }
        || condition is CmpExpr { Operation: CmpOperation.Uge or CmpOperation.Ugt, Left: Variable { Name: "argc" } };

    /// <summary>
    /// Проверяет, содержит ли тело цикла операции инкремента/декремента (признак цикла по указателю).
    /// Переопределяется в профилях для разной эвристики.
    /// </summary>
    protected virtual bool LoopBodyAdvancesPointer(ExprBlock bodyStart)
    {
        var ops = new List<Operation>();
        var visited = new HashSet<ExprBlock>();
        CollectOperationsStatic(bodyStart, ops, visited, stopBefore: null, maxBlocks: 8);
        return ops.Any(static op => op is IncOperation or DecOperation);
    }

    /// <summary>
    /// Проверяет, использует ли условие разыменование указателя (признак цикла по строке/массиву).
    /// </summary>
    protected static bool ConditionUsesCharPointerDeref(Expr condition)
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

    /// <summary>
    /// Собирает операции из блока с ограничением на количество блоков.
    /// Используется в эвристиках распознавания циклов.
    /// </summary>
    protected static void CollectOperationsStatic(
        ExprBlock? block,
        List<Operation> result,
        HashSet<ExprBlock> visited,
        ExprBlock? stopBefore,
        int maxBlocks)
    {
        var count = 0;
        while (block is not null && block != stopBefore && count++ < maxBlocks)
        {
            if (!visited.Add(block))
            {
                return;
            }

            result.AddRange(block.Operations);
            block = block.Next;
        }
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

    private static Expr InvertCondition(Expr condition) =>
        condition switch
        {
            CmpExpr cmp => cmp.Operation switch
            {
                CmpOperation.Eq => cmp with { Operation = CmpOperation.Ne },
                CmpOperation.Ne => cmp with { Operation = CmpOperation.Eq },
                CmpOperation.Ult => cmp with { Operation = CmpOperation.Uge },
                CmpOperation.Ule => cmp with { Operation = CmpOperation.Ugt },
                CmpOperation.Ugt => cmp with { Operation = CmpOperation.Ule },
                CmpOperation.Uge => cmp with { Operation = CmpOperation.Ult },
                _ => cmp,
            },
            _ => condition,
        };

}

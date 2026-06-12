namespace UltraDecompiler.Decompilation;

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
            }
        }
    }

    /// <summary>
    /// Рекурсивно собирает операции, начиная с <paramref name="block"/>, до <paramref name="stopBefore"/>
    /// или до повторного посещения блока (точка слияния / обратное ребро цикла).
    /// </summary>
    private static void CollectOperations(
        ExprBlock? block,
        List<Operation> result,
        HashSet<ExprBlock> visited,
        ExprBlock? stopBefore = null)
    {
        while (block != null && block != stopBefore)
        {
            if (!visited.Add(block))
                return;

            result.AddRange(block.Operations);
            if (EndsWithReturn(block.Operations))
            {
                return;
            }

            if (block.ConditionalBlock != null && block.Condition != null)
            {
                var merge = FindMerge(block.ConditionalBlock, block.Next);
                var thenStop = ReferenceEquals(merge, block.ConditionalBlock) ? null : merge;

                var thenBody = new List<Operation>();
                CollectOperations(block.ConditionalBlock, thenBody, visited, stopBefore: thenStop);

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
    private static HashSet<ExprBlock> CollectReachable(ExprBlock? start)
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

    /// <summary>Условие на заголовке цикла: ветка «истина» снова достигает этот блок.</summary>
    private static bool IsLoopHeader(ExprBlock block, ExprBlock? thenStart)
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
    private static bool MergeIsReturnOnly(ExprBlock merge, HashSet<ExprBlock> visited)
    {
        var probe = new List<Operation>();
        var probeVisited = new HashSet<ExprBlock>(visited);
        CollectOperations(merge, probe, probeVisited);
        return probe.Count > 0 && probe.All(static op => op is ReturnOperation);
    }

}

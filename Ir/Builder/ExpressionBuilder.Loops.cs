namespace UltraDecompiler.Ir.Builder;

public partial class ExpressionBuilder
{
    private readonly record struct LoopLayout(
        ExprBlock BodyStart,
        ExprBlock? ExitStart,
        Expr ContinueCondition);

    /// <summary>
    /// Определяет раскладку цикла QuickC: тело может идти по fallthrough (Next)
    /// или по ветке условия (ConditionalBlock).
    /// </summary>
    private bool TryGetLoopLayout(ExprBlock header, out LoopLayout layout)
    {
        layout = default;

        if (header.Condition is null)
        {
            return false;
        }

        if (header.Next is not null)
        {
            var bodyEntry = ResolveLoopBodyStart(header.Next);
            if (IsLoopHeader(header, bodyEntry))
            {
                layout = new LoopLayout(
                    bodyEntry,
                    header.ConditionalBlock,
                    !header.Condition);
                return true;
            }
        }

        if (header.ConditionalBlock is not null)
        {
            var bodyEntry = ResolveLoopBodyStart(header.ConditionalBlock);
            if (IsLoopHeader(header, bodyEntry))
            {
                layout = new LoopLayout(
                    bodyEntry,
                    header.Next,
                    header.Condition);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Пропускает пустые jmp-блоки QuickC /Od до реального входа в тело цикла.
    /// </summary>
    private static ExprBlock ResolveLoopBodyStart(ExprBlock backEdgeStart)
    {
        var block = backEdgeStart;

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

        return backEdgeStart;
    }

    /// <summary>
    /// Преобразует заголовок цикла QuickC в <see cref="ForOperation"/> или <see cref="WhileOperation"/>.
    /// </summary>
    private void EmitLoopOperation(
        ExprBlock header,
        ExprBlock? merge,
        List<Operation> result,
        HashSet<ExprBlock> visited,
        ExprBlock? enclosingLoopExit,
        ExprBlock? enclosingLoopHeader)
    {
        if (!TryGetLoopLayout(header, out var layout))
        {
            return;
        }

        var loopBody = new List<Operation>();
        var bodyVisited = new HashSet<ExprBlock>(visited);

        // do-while / bottom-tested: тело уже собрано линейно до заголовка.
        if (visited.Contains(layout.BodyStart))
        {
            if (ReferenceEquals(layout.BodyStart, header))
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
                var chain = CollectNextChain(layout.BodyStart, header);
                foreach (var chainBlock in chain)
                {
                    loopBody.AddRange(chainBlock.Operations);
                    bodyVisited.Add(chainBlock);
                }

                RemoveLinearChainFromResult(result, chain);
            }
        }
        else
        {
            CollectOperations(
                layout.BodyStart,
                loopBody,
                bodyVisited,
                stopBefore: header,
                enclosingLoopExit: layout.ExitStart,
                enclosingLoopHeader: header);
        }

        foreach (var block in bodyVisited)
        {
            visited.Add(block);
        }

        visited.Add(header);

        SanitizeLoopBody(loopBody);

        if (TryBuildForOperation(layout.ContinueCondition, loopBody, result, out var forOp))
        {
            result.Add(forOp);
        }
        else
        {
            if (TryBuildOxRegisterCounterLoop(
                    header,
                    layout.BodyStart,
                    layout.ExitStart,
                    loopBody,
                    result,
                    out var oxForOp,
                    out var oxSpillCounter))
            {
                result.Add(oxForOp);
                StripOxRegisterLoopSpillFromExit(layout.ExitStart, oxSpillCounter!);
                StripLoopPreamble(result, header);
                CollectLoopExitOperations(layout.ExitStart, merge, result, visited);
                return;
            }

            result.Add(new WhileOperation(layout.ContinueCondition, loopBody));
        }

        StripLoopPreamble(result, header);
        CollectLoopExitOperations(layout.ExitStart, merge, result, visited);
    }

    /// <summary>
    /// QuickC /Ox: цикл со счётчиком в регистре (SI/DI/BX/CX) вместо стековой переменной.
    /// Реализуется в <see cref="ExpressionBuilderQuickCOpt"/>.
    /// </summary>
    protected virtual bool TryBuildOxRegisterCounterLoop(
        ExprBlock header,
        ExprBlock bodyStart,
        ExprBlock? exitStart,
        List<Operation> loopBody,
        List<Operation> initSearchList,
        out ForOperation forOp,
        out Variable? spillCounterVar)
    {
        forOp = null!;
        spillCounterVar = null;
        return false;
    }

    /// <summary>
    /// Убирает служебный spill <c>mov [bp-N], reg</c> после Ox-цикла (счётчик уже в For).
    /// </summary>
    private static void StripOxRegisterLoopSpillFromExit(ExprBlock? exitStart, Variable counterVar)
    {
        if (exitStart is null)
        {
            return;
        }

        for (var i = exitStart.Operations.Count - 1; i >= 0; i--)
        {
            if (exitStart.Operations[i] is SetOperation { Dst: Variable dst }
                && ReferenceEquals(dst, counterVar))
            {
                exitStart.Operations.RemoveAt(i);
            }
        }
    }

    /// <summary>Блоки тела по цепочке fallthrough до заголовка цикла.</summary>
    private static List<ExprBlock> CollectNextChain(ExprBlock? start, ExprBlock? stopBefore)
    {
        var chain = new List<ExprBlock>();
        var block = start;

        while (block is not null && block != stopBefore)
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
            exitVisited.Add(epilogueMerge);
        }

        foreach (var block in exitVisited)
        {
            visited.Add(block);
        }

        result.AddRange(exitOps);
    }


    /// <summary>
    /// Распознаёт <c>if (cond) continue;</c> внутри тела цикла (ветка уходит на заголовок или шаг итерации).
    /// </summary>
    private bool TryEmitContinueIf(
        ExprBlock block,
        ExprBlock? loopHeader,
        ExprBlock? loopExit,
        ExprBlock? merge,
        List<Operation> result,
        HashSet<ExprBlock> visited)
    {
        if (!TryGetContinueBranch(block, loopHeader, out var onConditional))
        {
            return false;
        }

        visited.Add(block);

        var continueCondition = onConditional ? block.Condition! : !block.Condition!;
        result.Add(new IfOperation(continueCondition, [new ContinueOperation()]));

        var fallthrough = onConditional ? block.Next : block.ConditionalBlock;
        if (fallthrough is not null)
        {
            CollectOperations(
                fallthrough,
                result,
                visited,
                stopBefore: loopHeader ?? merge,
                enclosingLoopExit: loopExit,
                enclosingLoopHeader: loopHeader);
        }

        return true;
    }

    /// <summary>
    /// Заголовок сравнения, одна ветка ведёт на заголовок цикла — это continue, не вложенный цикл.
    /// </summary>
    private static bool IsContinueGuard(ExprBlock block, ExprBlock? loopHeader)
    {
        if (loopHeader is null || block.Condition is null)
        {
            return false;
        }

        // QuickC: continue обычно if (x == K) / if (x != K); заголовок вложенного for — j > N.
        if (!ConditionIsEqualityTest(block.Condition))
        {
            return false;
        }

        return TryGetContinueBranch(block, loopHeader, out _);
    }

    private static bool TryGetContinueBranch(
        ExprBlock block,
        ExprBlock? loopHeader,
        out bool continueOnConditional)
    {
        continueOnConditional = false;

        if (loopHeader is null)
        {
            return false;
        }

        if (block.ConditionalBlock is not null
            && ContinueTargetLoopsBack(block.ConditionalBlock, loopHeader)
            && (block.Next is null || !ContinueTargetLoopsBack(block.Next, loopHeader)))
        {
            continueOnConditional = true;
            return true;
        }

        if (block.Next is not null
            && ContinueTargetLoopsBack(block.Next, loopHeader)
            && (block.ConditionalBlock is null || !ContinueTargetLoopsBack(block.ConditionalBlock, loopHeader)))
        {
            continueOnConditional = false;
            return true;
        }

        return false;
    }

    private static bool ContinueTargetLoopsBack(ExprBlock? start, ExprBlock loopHeader)
    {
        if (start is null)
        {
            return false;
        }

        var visited = new HashSet<ExprBlock>();
        var block = start;

        for (var step = 0; step < 4 && block is not null; step++)
        {
            if (ReferenceEquals(block, loopHeader))
            {
                return true;
            }

            if (!visited.Add(block))
            {
                return false;
            }

            if (block.Operations.Count == 0 && ReferenceEquals(block.Next, loopHeader))
            {
                return true;
            }

            var instructions = block.BasicBlock.Instructions;
            if (instructions.Count == 1
                && instructions[0].IsUnconditionalJump
                && ReferenceEquals(block.Next, loopHeader))
            {
                return true;
            }

            // Тело цикла (sum += n и т.п.) — не continue-ветка.
            if (block.Operations.Count > 0 && !IsIterationStepBlock(block))
            {
                return false;
            }

            block = block.Next;
        }

        return false;
    }

    private static bool IsIterationStepBlock(ExprBlock block) =>
        block.Operations.Count > 0 && block.Operations.All(IsIterationStepOperation);

    private static bool IsIterationStepOperation(Operation op) =>
        op switch
        {
            IncOperation or DecOperation => true,
            AddAssignOperation { Value: ConstExpr } => true,
            SubAssignOperation { Value: ConstExpr } => true,
            SetOperation { Dst: Variable dst, Src: Math2Expr { First: Variable first, Second: ConstExpr } math }
                when ReferenceEquals(dst, first)
                && math.Operation is Math2Operation.Add or Math2Operation.Sub => true,
            _ => false,
        };

    /// <summary>
    /// Распознаёт <c>if (cond) break;</c> внутри тела цикла (ветка уходит на выход цикла).
    /// </summary>
    private bool TryEmitBreakIf(
        ExprBlock block,
        ExprBlock? loopExit,
        ExprBlock? loopHeader,
        ExprBlock? merge,
        List<Operation> result,
        HashSet<ExprBlock> visited)
    {
        if (loopExit is null || block.ConditionalBlock is null || block.Condition is null)
        {
            return false;
        }

        if (!BreakTargetReachesExit(block.ConditionalBlock, loopExit))
        {
            return false;
        }

        visited.Add(block);
        result.Add(new IfOperation(block.Condition, [new BreakOperation()]));

        if (block.Next is not null)
        {
            CollectOperations(
                block.Next,
                result,
                visited,
                stopBefore: loopHeader ?? merge,
                enclosingLoopExit: loopExit,
                enclosingLoopHeader: loopHeader);
        }

        return true;
    }

    /// <summary>
    /// Заголовок сравнения с константой, ветка «истина» ведёт на выход цикла — это break, не вложенный цикл.
    /// </summary>
    private static bool IsBreakGuard(ExprBlock block, ExprBlock? loopExit)
    {
        if (loopExit is null || block.ConditionalBlock is null || block.Condition is null)
        {
            return false;
        }

        if (!ConditionIsEqualityTest(block.Condition))
        {
            return false;
        }

        return BreakTargetReachesExit(block.ConditionalBlock, loopExit);
    }

    private static bool ConditionIsEqualityTest(Expr condition) =>
        condition is CmpExpr { Operation: CmpOperation.Eq or CmpOperation.Ne };

    private static bool BreakTargetReachesExit(ExprBlock? start, ExprBlock loopExit)
    {
        if (start is null)
        {
            return false;
        }

        var visited = new HashSet<ExprBlock>();
        var block = start;

        for (var step = 0; step < 4 && block is not null; step++)
        {
            if (ReferenceEquals(block, loopExit))
            {
                return true;
            }

            if (!visited.Add(block))
            {
                return false;
            }

            if (block.Operations.Count == 0 && ReferenceEquals(block.Next, loopExit))
            {
                return true;
            }

            var instructions = block.BasicBlock.Instructions;
            if (instructions.Count == 1
                && instructions[0].IsUnconditionalJump
                && ReferenceEquals(block.Next, loopExit))
            {
                return true;
            }

            block = block.Next ?? block.ConditionalBlock;
        }

        return false;
    }

    /// <summary>
    /// Пытается собрать классический счётный цикл QuickC /Od:
    /// init перед заголовком, шаг счётчика (±1 или ±N) в конце тела.
    /// </summary>
    private static bool TryBuildForOperation(
        Expr continueCondition,
        List<Operation> body,
        List<Operation> initSearchList,
        out ForOperation forOp)
    {
        forOp = null!;

        if (!TryGetLoopCounter(continueCondition, out var counter))
        {
            return false;
        }

        if (!TryExtractIteration(body, counter, out var iteration, out var bodyWithoutIteration))
        {
            return false;
        }

        if (!TryExtractInit(initSearchList, counter, out var init))
        {
            return false;
        }

        forOp = new ForOperation(init, continueCondition, iteration, bodyWithoutIteration);
        return true;
    }

    /// <summary>Извлекает переменную-счётчик из условия продолжения цикла.</summary>
    private static bool TryGetLoopCounter(Expr condition, out Variable counter)
    {
        if (condition is CmpExpr cmp)
        {
            return TryGetCounterFromCmp(cmp, out counter);
        }

        if (condition is Math1Expr { Operation: Math1Operation.Not, Op: CmpExpr inner })
        {
            return TryGetCounterFromCmp(inner, out counter);
        }

        counter = null!;
        return false;
    }

    private static bool TryGetCounterFromCmp(CmpExpr cmp, out Variable counter)
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

        counter = null!;
        return false;
    }

    /// <summary>
    /// Ищет присваивание счётчику непосредственно перед заголовком цикла
    /// (типичный init QuickC: <c>var = N; jmp header</c>).
    /// </summary>
    private static bool TryExtractInit(List<Operation> operations, Variable counter, out SetOperation init)
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
    /// Выделяет шаг итерации (inc/dec или <c>var = var ± N</c>) с конца тела цикла.
    /// </summary>
    private static bool TryExtractIteration(
        List<Operation> body,
        Variable counter,
        out Operation iteration,
        out IReadOnlyList<Operation> bodyWithoutIteration)
    {
        iteration = null!;
        bodyWithoutIteration = body;

        if (body.Count == 0)
        {
            return false;
        }

        var last = body[^1];
        if (last is IncOperation inc && ExprReferencesVariable(inc.Target, counter))
        {
            iteration = last;
            bodyWithoutIteration = body.Take(body.Count - 1).ToList();
            return true;
        }

        if (last is DecOperation dec && ExprReferencesVariable(dec.Target, counter))
        {
            iteration = last;
            bodyWithoutIteration = body.Take(body.Count - 1).ToList();
            return true;
        }

        if (last is AddAssignOperation addAssign && ExprReferencesVariable(addAssign.Target, counter))
        {
            iteration = last;
            bodyWithoutIteration = body.Take(body.Count - 1).ToList();
            return true;
        }

        if (last is SubAssignOperation subAssign && ExprReferencesVariable(subAssign.Target, counter))
        {
            iteration = last;
            bodyWithoutIteration = body.Take(body.Count - 1).ToList();
            return true;
        }

        if (last is SetOperation set && TryMatchCounterStep(set, counter, out iteration))
        {
            bodyWithoutIteration = body.Take(body.Count - 1).ToList();
            return true;
        }

        // QuickC /Od: add [bp+N], K → temp = var + K; var = temp
        if (body.Count >= 2
            && TryMatchCounterStepViaTemp(body[^2], last, counter, out iteration))
        {
            bodyWithoutIteration = body.Take(body.Count - 2).ToList();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Распознаёт шаг счётчика через временную переменную (типично для <c>add word ptr [bp-N], K</c>, K != 1).
    /// </summary>
    private static bool TryMatchCounterStepViaTemp(
        Operation prev,
        Operation last,
        Variable counter,
        out Operation iteration)
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

        if (prev is not SetOperation { Dst: Variable tempDst, Src: Math2Expr math } tempAssign)
        {
            return false;
        }

        if (!ReferenceEquals(tempDst, temp))
        {
            return false;
        }

        if (!TryMatchCounterStep(tempAssign with { Dst = counter }, counter, out iteration))
        {
            return false;
        }

        iteration = new SetOperation(dst, math);
        return true;
    }

    /// <summary>Проверяет, что операция — это <c>counter = counter ± N</c> (N — ненулевая константа).</summary>
    private static bool TryMatchCounterStep(SetOperation set, Variable counter, out Operation iteration)
    {
        iteration = set;

        if (!ExprReferencesVariable(set.Dst, counter))
        {
            return false;
        }

        if (set.Src is not Math2Expr { First: var first, Second: ConstExpr delta } math2)
        {
            return false;
        }

        if (delta.Value == 0 || !ExprReferencesVariable(first, counter))
        {
            return false;
        }

        return math2.Operation is Math2Operation.Add or Math2Operation.Sub;
    }

    private static bool SameVariable(Variable left, Variable right) =>
        ReferenceEquals(left, right)
        || (left.IsStack && right.IsStack && left.Number == right.Number);

    private static bool ExprReferencesVariable(Expr expr, Variable counter) =>
        expr is Variable variable && SameVariable(variable, counter);
}

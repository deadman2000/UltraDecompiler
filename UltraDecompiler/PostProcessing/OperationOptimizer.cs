using UltraDecompiler.Decompilation;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Упрощает плоский список IR-операций перед генерацией C-кода:
/// удаляет лишние копии переменных и неиспользуемые результаты вызовов.
/// </summary>
public static class OperationOptimizer
{
    /// <summary>
    /// Оптимизирует дерево операций (рекурсивно для if/while/for).
    /// </summary>
    public static IReadOnlyList<Operation> Optimize(IReadOnlyList<Operation> operations) =>
        OptimizeList(operations.ToList());

    private static List<Operation> OptimizeList(List<Operation> operations)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = OptimizeNested(operations[i]);
        }

        var changed = true;
        while (changed)
        {
            changed = false;

            for (var i = 0; i < operations.Count; i++)
            {
                if (TryFoldSelfAssignTempStore(operations, i))
                {
                    i = Math.Max(-1, i - 2);
                    changed = true;
                    continue;
                }

                if (operations[i] is not SetOperation set)
                {
                    continue;
                }

                if (set.Src is Variable tempVar
                    && TryFindTempExpressionAssignment(operations, i, tempVar, out var exprIndex, out var expr)
                    && CanFoldTempIntoAssignment(operations, exprIndex, i, set.Dst, expr))
                {
                    var assignIndex = i;
                    operations.RemoveAt(exprIndex);
                    if (exprIndex < assignIndex)
                    {
                        assignIndex--;
                    }

                    operations[assignIndex] = new SetOperation(set.Dst, expr);
                    i = assignIndex - 1;
                    changed = true;
                    continue;
                }

                if (IsTailAssignmentForLoopHeader(operations, i, set.Dst))
                {
                    continue;
                }

                if (!IsVariableReadAfter(operations, i, set.Dst))
                {
                    if (IsVariableUsedInEarlierSetSource(operations, i, set.Dst))
                    {
                        continue;
                    }

                    if (set.Src is CallExpr call)
                    {
                        operations[i] = new CallOperation(call.Name, call.Args);
                    }
                    else
                    {
                        operations.RemoveAt(i);
                        i--;
                    }

                    changed = true;
                    continue;
                }

                if (set.Src is Variable srcVar
                    && CanPropagateCopy(operations, i, set.Dst, srcVar, GetCopyPropagationEnd(operations, i, set.Dst)))
                {
                    var propagateEnd = GetCopyPropagationEnd(operations, i, set.Dst);
                    SubstituteVariable(operations, i + 1, propagateEnd, set.Dst, srcVar);
                    operations.RemoveAt(i);
                    i--;
                    changed = true;
                    continue;
                }

                if (CanPropagateToReturn(operations, i, set))
                {
                    var propagateEnd = GetCopyPropagationEnd(operations, i, set.Dst);
                    SubstituteVariable(operations, i + 1, propagateEnd, set.Dst, set.Src);
                    operations.RemoveAt(i);
                    i--;
                    changed = true;
                }
            }
        }

        return operations;
    }

    private static Operation OptimizeNested(Operation operation) =>
        operation switch
        {
            IfOperation branch => new IfOperation(
                branch.Condition,
                OptimizeList(branch.ThenBody.ToList()),
                branch.ElseBody is not null ? OptimizeList(branch.ElseBody.ToList()) : null),
            WhileOperation loop => new WhileOperation(loop.Condition, OptimizeList(loop.Body.ToList())),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? OptimizeNested(loop.Init) : null,
                loop.Condition,
                loop.Iteration is not null ? OptimizeNested(loop.Iteration) : null,
                OptimizeList(loop.Body.ToList())),
            _ => operation,
        };

    /// <summary>
    /// Конец полуоткрытого интервала [copyIndex+1, end) для подстановки копии:
    /// до следующего присваивания <paramref name="dst"/>, иначе до конца списка.
    /// </summary>
    private static int GetCopyPropagationEnd(IReadOnlyList<Operation> operations, int copyIndex, Variable dst)
    {
        var nextDef = FindNextDefinitionIndex(operations, copyIndex, dst);
        return nextDef >= 0 ? nextDef : operations.Count;
    }

    private static bool CanPropagateCopy(
        IReadOnlyList<Operation> operations,
        int copyIndex,
        Variable dst,
        Variable src,
        int propagateEnd)
    {
        var lastReadIndex = FindLastReadIndexInRange(operations, copyIndex, propagateEnd, dst);
        if (lastReadIndex < 0)
        {
            return !IsTailAssignmentForLoopHeader(operations, copyIndex, dst);
        }

        return !DefinesVariableBetween(operations, copyIndex, lastReadIndex, src);
    }

    private static int FindNextDefinitionIndex(IReadOnlyList<Operation> operations, int fromIndex, Variable variable)
    {
        for (var i = fromIndex + 1; i < operations.Count; i++)
        {
            if (operations[i] is SetOperation set && set.Dst.Number == variable.Number)
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindLastReadIndexInRange(
        IReadOnlyList<Operation> operations,
        int fromIndex,
        int toExclusive,
        Variable variable)
    {
        var last = -1;

        for (var i = fromIndex + 1; i < toExclusive && i < operations.Count; i++)
        {
            if (ReadsVariableDeep(operations[i], variable))
            {
                last = i;
            }
        }

        return last;
    }

    /// <summary>
    /// Ищет единственное присваивание <c>temp = expr</c> перед <c>dst = temp</c>.
    /// </summary>
    private static bool TryFindTempExpressionAssignment(
        IReadOnlyList<Operation> operations,
        int assignIndex,
        Variable temp,
        out int exprIndex,
        out Expr expr)
    {
        exprIndex = -1;
        expr = null!;

        for (var j = assignIndex - 1; j >= 0; j--)
        {
            if (operations[j] is not SetOperation candidate
                || candidate.Dst.Number != temp.Number
                || !IsFoldableTempExpression(candidate.Src))
            {
                continue;
            }

            if (FindNextDefinitionIndex(operations, j, temp) >= 0)
            {
                continue;
            }

            if (FindLastReadIndexInRange(operations, j, assignIndex + 1, temp) != assignIndex)
            {
                continue;
            }

            exprIndex = j;
            expr = candidate.Src;
            return true;
        }

        return false;
    }

    private static bool IsFoldableTempExpression(Expr expr) =>
        expr switch
        {
            Variable or ConstExpr or StringExpr or CallExpr or ImageOffsetExpr => false,
            _ => true,
        };

    private static bool CanFoldTempIntoAssignment(
        IReadOnlyList<Operation> operations,
        int exprIndex,
        int assignIndex,
        Variable dst,
        Expr expr)
    {
        if (ExprSubstitution.Contains(expr, dst)
            && DefinesVariableBetween(operations, 0, exprIndex - 1, dst))
        {
            return false;
        }

        foreach (var variable in ExprSubstitution.CollectVariables(expr))
        {
            if (variable.Number == dst.Number)
            {
                continue;
            }

            if (DefinesVariableBetween(operations, exprIndex, assignIndex - 1, variable))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Разрешает подставить выражение из SetOperation напрямую в return,
    /// если результат используется только там и переменные выражения не переопределяются.
    /// </summary>
    private static bool CanPropagateToReturn(
        IReadOnlyList<Operation> operations,
        int setIndex,
        SetOperation set)
    {
        if (!IsOnlyUsedInReturn(operations, setIndex, set.Dst))
        {
            return false;
        }

        var lastReadIndex = FindLastReadIndex(operations, setIndex, set.Dst);
        if (lastReadIndex < 0)
        {
            return true;
        }

        foreach (var variable in ExprSubstitution.CollectVariables(set.Src))
        {
            if (DefinesVariableBetween(operations, setIndex, lastReadIndex, variable))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsOnlyUsedInReturn(
        IReadOnlyList<Operation> operations,
        int defIndex,
        Variable variable)
    {
        for (var i = defIndex + 1; i < operations.Count; i++)
        {
            if (ReadsVariableOutsideReturn(operations[i], variable))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ReadsVariableOutsideReturn(Operation operation, Variable variable) =>
        operation switch
        {
            ReturnOperation => false,
            SetOperation set => ExprSubstitution.Contains(set.Src, variable),
            CallOperation call => call.Args.Any(arg => ExprSubstitution.Contains(arg, variable)),
            StoreOperation store => ExprSubstitution.Contains(store.Address, variable)
                || ExprSubstitution.Contains(store.Segment, variable)
                || ExprSubstitution.Contains(store.Value, variable),
            IncOperation inc => ExprSubstitution.Contains(inc.Target, variable)
                || ExprSubstitution.Contains(inc.Segment, variable),
            DecOperation dec => ExprSubstitution.Contains(dec.Target, variable)
                || ExprSubstitution.Contains(dec.Segment, variable),
            IfOperation branch => ExprSubstitution.Contains(branch.Condition, variable)
                || branch.ThenBody.Any(op => ReadsVariableOutsideReturn(op, variable))
                || (branch.ElseBody?.Any(op => ReadsVariableOutsideReturn(op, variable)) ?? false),
            WhileOperation loop => ExprSubstitution.Contains(loop.Condition, variable)
                || loop.Body.Any(op => ReadsVariableOutsideReturn(op, variable)),
            ForOperation loop => (loop.Init is not null && ReadsVariableOutsideReturn(loop.Init, variable))
                || ExprSubstitution.Contains(loop.Condition, variable)
                || (loop.Iteration is not null && ReadsVariableOutsideReturn(loop.Iteration, variable))
                || loop.Body.Any(op => ReadsVariableOutsideReturn(op, variable)),
            _ => false,
        };

    private static int FindLastReadIndex(IReadOnlyList<Operation> operations, int fromIndex, Variable variable)
    {
        var last = -1;

        for (var i = fromIndex + 1; i < operations.Count; i++)
        {
            if (ReadsVariableDeep(operations[i], variable))
            {
                last = i;
            }
        }

        return last;
    }

    private static bool IsVariableReadAfter(IReadOnlyList<Operation> operations, int defIndex, Variable variable) =>
        FindLastReadIndex(operations, defIndex, variable) >= 0;

    /// <summary>
    /// Присваивание в конце тела цикла нужно для чтения в начале тела на следующей итерации.
    /// </summary>
    private static bool IsTailAssignmentForLoopHeader(
        IReadOnlyList<Operation> operations,
        int defIndex,
        Variable variable)
    {
        if (defIndex <= 0
            || FindLastReadIndex(operations, defIndex, variable) >= 0
            || DefinesVariableDeep(operations[0], variable)
            || !ReadsVariableDeep(operations[0], variable))
        {
            return false;
        }

        return !DefinesVariableBetween(operations, 0, defIndex - 1, variable);
    }

    /// <summary>
    /// Переопределение переменной нельзя удалять как мёртвый код, если она участвует
    /// в выражении более раннего SetOperation (иначе ломается проверка CanPropagateToReturn).
    /// </summary>
    private static bool IsVariableUsedInEarlierSetSource(
        IReadOnlyList<Operation> operations,
        int defIndex,
        Variable variable)
    {
        for (var i = 0; i < defIndex; i++)
        {
            if (operations[i] is SetOperation earlier
                && ExprSubstitution.Contains(earlier.Src, variable))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DefinesVariableBetween(
        IReadOnlyList<Operation> operations,
        int fromExclusive,
        int toInclusive,
        Variable variable)
    {
        for (var i = fromExclusive + 1; i <= toInclusive && i < operations.Count; i++)
        {
            if (DefinesVariableDeep(operations[i], variable))
            {
                return true;
            }
        }

        return false;
    }

    private static void SubstituteVariable(List<Operation> operations, int fromIndex, int toExclusive, Variable from, Expr to)
    {
        for (var i = fromIndex; i < toExclusive && i < operations.Count; i++)
        {
            operations[i] = SubstituteInOperation(operations[i], from, to);
        }
    }

    private static Operation SubstituteInOperation(Operation operation, Variable from, Expr to) =>
        operation switch
        {
            SetOperation set => new SetOperation(set.Dst, ExprSubstitution.Replace(set.Src, from, to)),
            CallOperation call => new CallOperation(
                call.Name,
                call.Args.Select(arg => ExprSubstitution.Replace(arg, from, to)).ToList()),
            StoreOperation store => new StoreOperation(
                ExprSubstitution.Replace(store.Address, from, to),
                store.Segment is null ? null : ExprSubstitution.Replace(store.Segment, from, to),
                ExprSubstitution.Replace(store.Value, from, to)),
            IncOperation inc => new IncOperation(
                ReplaceIncDecTarget(inc.Target, from, to),
                inc.Segment is null ? null : ExprSubstitution.Replace(inc.Segment, from, to)),
            DecOperation dec => new DecOperation(
                ReplaceIncDecTarget(dec.Target, from, to),
                dec.Segment is null ? null : ExprSubstitution.Replace(dec.Segment, from, to)),
            ReturnOperation ret => new ReturnOperation(
                ret.Value is null ? null : ExprSubstitution.Replace(ret.Value, from, to)),
            IfOperation branch => new IfOperation(
                ExprSubstitution.Replace(branch.Condition, from, to),
                branch.ThenBody.Select(op => SubstituteInOperation(op, from, to)).ToList(),
                branch.ElseBody?.Select(op => SubstituteInOperation(op, from, to)).ToList()),
            WhileOperation loop => new WhileOperation(
                ExprSubstitution.Replace(loop.Condition, from, to),
                loop.Body.Select(op => SubstituteInOperation(op, from, to)).ToList()),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? SubstituteInOperation(loop.Init, from, to) : null,
                loop.Condition is null ? null : ExprSubstitution.Replace(loop.Condition, from, to),
                loop.Iteration is not null ? SubstituteInOperation(loop.Iteration, from, to) : null,
                loop.Body.Select(op => SubstituteInOperation(op, from, to)).ToList()),
            _ => operation,
        };

    private static bool ReadsVariableDeep(Operation operation, Variable variable) =>
        operation switch
        {
            SetOperation set => ExprSubstitution.Contains(set.Src, variable),
            CallOperation call => call.Args.Any(arg => ExprSubstitution.Contains(arg, variable)),
            StoreOperation store => ExprSubstitution.Contains(store.Address, variable)
                || ExprSubstitution.Contains(store.Segment, variable)
                || ExprSubstitution.Contains(store.Value, variable),
            IncOperation inc => ExprSubstitution.Contains(inc.Target, variable)
                || ExprSubstitution.Contains(inc.Segment, variable),
            DecOperation dec => ExprSubstitution.Contains(dec.Target, variable)
                || ExprSubstitution.Contains(dec.Segment, variable),
            ReturnOperation ret => ExprSubstitution.Contains(ret.Value, variable),
            IfOperation branch => ExprSubstitution.Contains(branch.Condition, variable)
                || branch.ThenBody.Any(op => ReadsVariableDeep(op, variable))
                || (branch.ElseBody?.Any(op => ReadsVariableDeep(op, variable)) ?? false),
            WhileOperation loop => ExprSubstitution.Contains(loop.Condition, variable)
                || loop.Body.Any(op => ReadsVariableDeep(op, variable)),
            ForOperation loop => (loop.Init is not null && ReadsVariableDeep(loop.Init, variable))
                || ExprSubstitution.Contains(loop.Condition, variable)
                || (loop.Iteration is not null && ReadsVariableDeep(loop.Iteration, variable))
                || loop.Body.Any(op => ReadsVariableDeep(op, variable)),
            _ => false,
        };

    /// <summary>
    /// <c>temp = local ± 1; local = temp</c> → <c>local = local ± 1</c> (mov/add/mov из QuickC).
    /// </summary>
    private static bool TryFoldSelfAssignTempStore(List<Operation> operations, int assignIndex)
    {
        if (assignIndex <= 0
            || operations[assignIndex] is not SetOperation { Src: Variable temp, Dst: Variable dst }
            || operations[assignIndex - 1] is not SetOperation { Dst: Variable tempDef, Src: Math2Expr math }
            || tempDef.Number != temp.Number
            || math.First is not Variable local
            || local.Number != dst.Number
            || math.Second is not ConstExpr { Value: 1 }
            || math.Operation is not (Math2Operation.Add or Math2Operation.Sub))
        {
            return false;
        }

        operations[assignIndex - 1] = new SetOperation(dst, math);
        operations.RemoveAt(assignIndex);
        return true;
    }

    private static Expr ReplaceIncDecTarget(Expr target, Variable from, Expr to)
    {
        if (target is Variable variable && variable.Number == from.Number)
        {
            return target;
        }

        return ExprSubstitution.Replace(target, from, to);
    }

    private static bool DefinesVariableDeep(Operation operation, Variable variable) =>
        operation switch
        {
            SetOperation set => set.Dst.Number == variable.Number,
            IfOperation branch => branch.ThenBody.Any(op => DefinesVariableDeep(op, variable))
                || (branch.ElseBody?.Any(op => DefinesVariableDeep(op, variable)) ?? false),
            WhileOperation loop => loop.Body.Any(op => DefinesVariableDeep(op, variable)),
            ForOperation loop => (loop.Init is not null && DefinesVariableDeep(loop.Init, variable))
                || loop.Body.Any(op => DefinesVariableDeep(op, variable))
                || (loop.Iteration is not null && DefinesVariableDeep(loop.Iteration, variable)),
            IncOperation inc when inc.Target is Variable target => target.Number == variable.Number,
            DecOperation dec when dec.Target is Variable target => target.Number == variable.Number,
            _ => false,
        };
}

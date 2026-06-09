namespace UltraDecompiler.Decompilation;

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
                if (operations[i] is not SetOperation set)
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
                    && CanPropagateCopy(operations, i, set.Dst, srcVar))
                {
                    SubstituteVariable(operations, i + 1, set.Dst, srcVar);
                    operations.RemoveAt(i);
                    i--;
                    changed = true;
                    continue;
                }

                if (CanPropagateToReturn(operations, i, set))
                {
                    SubstituteVariable(operations, i + 1, set.Dst, set.Src);
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

    private static bool CanPropagateCopy(
        IReadOnlyList<Operation> operations,
        int copyIndex,
        Variable dst,
        Variable src)
    {
        var lastReadIndex = FindLastReadIndex(operations, copyIndex, dst);
        if (lastReadIndex < 0)
        {
            return true;
        }

        return !DefinesVariableBetween(operations, copyIndex, lastReadIndex, src);
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

    private static void SubstituteVariable(List<Operation> operations, int fromIndex, Variable from, Expr to)
    {
        for (var i = fromIndex; i < operations.Count; i++)
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
            _ => false,
        };
}

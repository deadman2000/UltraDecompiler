namespace UltraDecompiler.PostProcessing.Normalization;

/// <summary>
/// Подставляет <c>temp = expr</c> напрямую в места использования,
/// чтобы избежать объявления временных переменных в C-коде.
/// Это критично для round-trip: QuickC не генерирует промежуточные temp.
/// </summary>
public static class TempVariableEliminator
{
    /// <summary>
    /// Устраняет временные переменные из списка операций.
    /// </summary>
    public static IReadOnlyList<Operation> Eliminate(IReadOnlyList<Operation> operations) =>
        EliminateList(operations.ToList());

    private static List<Operation> EliminateList(List<Operation> operations)
    {
        var changed = true;
        while (changed)
        {
            changed = false;

            for (var i = 0; i < operations.Count; i++)
            {
                if (operations[i] is not SetOperation { Dst: Variable temp, Src: var expr }
                    || !temp.IsTemp)
                {
                    continue;
                }

                // Находим все использования temp
                var uses = FindTempUses(operations, i, temp);
                if (uses.Count == 0)
                {
                    // Мёртвое присваивание — удаляем
                    operations.RemoveAt(i);
                    i--;
                    changed = true;
                    continue;
                }

                // expr не должно иметь побочных эффектов, которые изменятся при подстановке
                // Если несколько использований, нельзя дублировать вызов
                if (HasSideEffects(expr) && uses.Count > 1)
                {
                    continue;
                }

                // Проверяем, можно ли безопасно подставить expr вместо всех использований
                if (CanSafelySubstitute(operations, i, uses, temp, expr))
                {
                    // Подставляем expr вместо всех использований temp
                    foreach (var useIndex in uses.OrderByDescending(idx => idx))
                    {
                        SubstituteTempAt(operations, useIndex, temp, expr);
                    }

                    // Удаляем присваивание temp
                    operations.RemoveAt(i);
                    i--;
                    changed = true;
                }
            }
        }

        // Рекурсивно обрабатываем вложенные структуры
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = EliminateNested(operations[i]);
        }

        return operations;
    }

    private static Operation EliminateNested(Operation operation) =>
        operation switch
        {
            IfOperation branch => new IfOperation(
                branch.Condition,
                EliminateList(branch.ThenBody.ToList()),
                branch.ElseBody is not null ? EliminateList(branch.ElseBody.ToList()) : null),
            WhileOperation loop => new WhileOperation(
                loop.Condition,
                EliminateList(loop.Body.ToList())),
            DoWhileOperation loop => new DoWhileOperation(
                loop.Condition,
                EliminateList(loop.Body.ToList())),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? EliminateNested(loop.Init) : null,
                loop.Condition,
                loop.Iteration is not null ? EliminateNested(loop.Iteration) : null,
                EliminateList(loop.Body.ToList())),
            SwitchOperation sw => new SwitchOperation(
                sw.Discriminant,
                sw.Cases.Select(c => new SwitchCase(
                    c.Value,
                    EliminateList([.. c.Body]))).ToList()),
            _ => operation,
        };

    /// <summary>
    /// Находит индексы операций, где используется temp.
    /// </summary>
    private static List<int> FindTempUses(IReadOnlyList<Operation> operations, int defIndex, Variable temp)
    {
        var uses = new List<int>();

        for (var i = defIndex + 1; i < operations.Count; i++)
        {
            if (ReadsVariableDeep(operations[i], temp))
            {
                uses.Add(i);
            }

            // Если temp переопределяется, дальнейшие использования не относятся к этому определению
            if (DefinesVariableDeep(operations[i], temp))
            {
                break;
            }
        }

        return uses;
    }

    /// <summary>
    /// Проверяет, можно ли безопасно подставить expr вместо temp.
    /// </summary>
    private static bool CanSafelySubstitute(
        IReadOnlyList<Operation> operations,
        int defIndex,
        List<int> uses,
        Variable temp,
        Expr expr)
    {
        // expr не должно содержать temp (циклическая зависимость)
        if (ExprSubstitution.Contains(expr, temp))
        {
            return false;
        }

        // Переменные в expr не должны переопределяться между определением и использованиями
        var variablesInExpr = ExprSubstitution.CollectVariables(expr);
        foreach (var variable in variablesInExpr)
        {
            if (ReferenceEquals(variable, temp))
            {
                continue;
            }

            foreach (var useIndex in uses)
            {
                if (DefinesVariableBetween(operations, defIndex, useIndex, variable))
                {
                    return false;
                }
            }
        }

        // expr не должно иметь побочных эффектов, которые изменятся при подстановке
        // (например, вызовы функций — но они обрабатываются отдельно)
        if (HasSideEffects(expr))
        {
            // Если несколько использований, нельзя дублировать вызов
            if (uses.Count > 1)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Проверяет, имеет ли выражение побочные эффекты.
    /// </summary>
    private static bool HasSideEffects(Expr expr) =>
        expr switch
        {
            CallExpr => true,
            IncDecExpr => true,
            Math1Expr unary => HasSideEffects(unary.Op),
            Math2Expr binary => HasSideEffects(binary.First) || HasSideEffects(binary.Second),
            _ => false,
        };

    /// <summary>
    /// Подставляет expr вместо temp в операции по индексу.
    /// </summary>
    private static void SubstituteTempAt(List<Operation> operations, int index, Variable temp, Expr expr)
    {
        operations[index] = SubstituteInOperation(operations[index], temp, expr);
    }

    private static Operation SubstituteInOperation(Operation operation, Variable from, Expr to) =>
        operation switch
        {
            SetOperation set => new SetOperation(
                ExprSubstitution.Replace(set.Dst, from, to),
                ExprSubstitution.Replace(set.Src, from, to)),
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
            AddAssignOperation add => new AddAssignOperation(
                ExprSubstitution.Replace(add.Target, from, to),
                ExprSubstitution.Replace(add.Value, from, to),
                add.Segment is null ? null : ExprSubstitution.Replace(add.Segment, from, to)),
            SubAssignOperation sub => new SubAssignOperation(
                ExprSubstitution.Replace(sub.Target, from, to),
                ExprSubstitution.Replace(sub.Value, from, to),
                sub.Segment is null ? null : ExprSubstitution.Replace(sub.Segment, from, to)),
            ReturnOperation ret => new ReturnOperation(
                ret.Value is null ? null : ExprSubstitution.Replace(ret.Value, from, to),
                ret.IsExplicit),
            IfOperation branch => new IfOperation(
                ExprSubstitution.Replace(branch.Condition, from, to),
                branch.ThenBody.Select(op => SubstituteInOperation(op, from, to)).ToList(),
                branch.ElseBody?.Select(op => SubstituteInOperation(op, from, to)).ToList()),
            WhileOperation loop => new WhileOperation(
                ExprSubstitution.Replace(loop.Condition, from, to),
                loop.Body.Select(op => SubstituteInOperation(op, from, to)).ToList()),
            DoWhileOperation loop => new DoWhileOperation(
                ExprSubstitution.Replace(loop.Condition, from, to),
                loop.Body.Select(op => SubstituteInOperation(op, from, to)).ToList()),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? SubstituteInOperation(loop.Init, from, to) : null,
                loop.Condition is null ? null : ExprSubstitution.Replace(loop.Condition, from, to),
                loop.Iteration is not null ? SubstituteInOperation(loop.Iteration, from, to) : null,
                loop.Body.Select(op => SubstituteInOperation(op, from, to)).ToList()),
            SwitchOperation sw => new SwitchOperation(
                ExprSubstitution.Replace(sw.Discriminant, from, to),
                sw.Cases.Select(c => new SwitchCase(
                    c.Value,
                    c.Body.Select(op => SubstituteInOperation(op, from, to)).ToList())).ToList()),
            CallOperation call => new CallOperation(
                call.Name,
                call.Args.Select(arg => ExprSubstitution.Replace(arg, from, to)).ToList()),
            _ => operation,
        };

    private static Expr ReplaceIncDecTarget(Expr target, Variable from, Expr to)
    {
        if (target is Variable variable && ReferenceEquals(variable, from))
        {
            return target;
        }

        return ExprSubstitution.Replace(target, from, to);
    }

    private static bool ReadsVariableDeep(Operation operation, Variable variable) =>
        operation switch
        {
            SetOperation set => ExprSubstitution.Contains(set.Dst, variable)
                || ExprSubstitution.Contains(set.Src, variable),
            CallOperation call => call.Args.Any(arg => ExprSubstitution.Contains(arg, variable)),
            StoreOperation store => ExprSubstitution.Contains(store.Address, variable)
                || ExprSubstitution.Contains(store.Segment, variable)
                || ExprSubstitution.Contains(store.Value, variable),
            IncOperation inc => ExprSubstitution.Contains(inc.Target, variable)
                || ExprSubstitution.Contains(inc.Segment, variable),
            DecOperation dec => ExprSubstitution.Contains(dec.Target, variable)
                || ExprSubstitution.Contains(dec.Segment, variable),
            AddAssignOperation add => ExprSubstitution.Contains(add.Target, variable)
                || (add.Segment is not null && ExprSubstitution.Contains(add.Segment, variable))
                || ExprSubstitution.Contains(add.Value, variable),
            SubAssignOperation sub => ExprSubstitution.Contains(sub.Target, variable)
                || (sub.Segment is not null && ExprSubstitution.Contains(sub.Segment, variable))
                || ExprSubstitution.Contains(sub.Value, variable),
            ReturnOperation ret => ExprSubstitution.Contains(ret.Value, variable),
            IfOperation branch => ExprSubstitution.Contains(branch.Condition, variable)
                || branch.ThenBody.Any(op => ReadsVariableDeep(op, variable))
                || (branch.ElseBody?.Any(op => ReadsVariableDeep(op, variable)) ?? false),
            WhileOperation loop => ExprSubstitution.Contains(loop.Condition, variable)
                || loop.Body.Any(op => ReadsVariableDeep(op, variable)),
            DoWhileOperation loop => ExprSubstitution.Contains(loop.Condition, variable)
                || loop.Body.Any(op => ReadsVariableDeep(op, variable)),
            ForOperation loop => (loop.Init is not null && ReadsVariableDeep(loop.Init, variable))
                || ExprSubstitution.Contains(loop.Condition, variable)
                || (loop.Iteration is not null && ReadsVariableDeep(loop.Iteration, variable))
                || loop.Body.Any(op => ReadsVariableDeep(op, variable)),
            SwitchOperation sw => sw.Cases.Any(c => c.Body.Any(op => ReadsVariableDeep(op, variable)))
                || ExprSubstitution.Contains(sw.Discriminant, variable),
            _ => false,
        };

    private static bool DefinesVariableDeep(Operation operation, Variable variable) =>
        operation switch
        {
            SetOperation set => AssignmentTarget.DefinesVariable(set.Dst, variable),
            IfOperation branch => branch.ThenBody.Any(op => DefinesVariableDeep(op, variable))
                || (branch.ElseBody?.Any(op => DefinesVariableDeep(op, variable)) ?? false),
            WhileOperation loop => loop.Body.Any(op => DefinesVariableDeep(op, variable)),
            DoWhileOperation loop => loop.Body.Any(op => DefinesVariableDeep(op, variable)),
            ForOperation loop => (loop.Init is not null && DefinesVariableDeep(loop.Init, variable))
                || loop.Body.Any(op => DefinesVariableDeep(op, variable))
                || (loop.Iteration is not null && DefinesVariableDeep(loop.Iteration, variable)),
            SwitchOperation sw => sw.Cases.Any(c => c.Body.Any(op => DefinesVariableDeep(op, variable))),
            IncOperation inc when inc.Target is Variable target => ReferenceEquals(target, variable),
            DecOperation dec when dec.Target is Variable target => ReferenceEquals(target, variable),
            AddAssignOperation add => AssignmentTarget.DefinesVariable(add.Target, variable),
            SubAssignOperation sub => AssignmentTarget.DefinesVariable(sub.Target, variable),
            _ => false,
        };

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
}

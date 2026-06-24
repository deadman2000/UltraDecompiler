using UltraDecompiler.Ir.Helpers;
using UltraDecompiler.PostProcessing.Infrastructure;

namespace UltraDecompiler.PostProcessing.Normalization;

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

                if (TryFoldTempIntoStoreAddress(operations, i))
                {
                    i = Math.Max(-1, i - 1);
                    changed = true;
                    continue;
                }

                if (TrySubstituteTempInNextOperation(operations, i))
                {
                    i = Math.Max(-1, i - 1);
                    changed = true;
                    continue;
                }

                if (operations[i] is not SetOperation set
                    || !AssignmentTarget.TryGetVariable(set.Dst, out var dstVar))
                {
                    continue;
                }

                if (set.Src is VariableExpr { Var: { IsTemp: true } tempVar }
                    && TryFindTempExpressionAssignment(operations, i, tempVar, out var exprIndex, out var expr)
                    && CanFoldTempIntoAssignment(operations, exprIndex, i, dstVar, expr))
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
                if (!IsVariableReadAfter(operations, i, dstVar))
                {
                    if (dstVar.IsStack)
                    {
                        continue;
                    }

                    if (IsVariableUsedInEarlierSetSource(operations, i, dstVar))
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

                if (!dstVar.IsStack
                    && set.Src is VariableExpr { Var: var srcVar }
                    && CanPropagateCopy(operations, i, dstVar, srcVar, GetCopyPropagationEnd(operations, i, dstVar)))
                {
                    var propagateEnd = GetCopyPropagationEnd(operations, i, dstVar);
                    SubstituteVariable(operations, i + 1, propagateEnd, dstVar, srcVar.ToGet());
                    operations.RemoveAt(i);
                    i--;
                    changed = true;
                    continue;
                }

                if (!dstVar.IsStack && CanPropagateToReturn(operations, i, set))
                {
                    var propagateEnd = GetCopyPropagationEnd(operations, i, dstVar);
                    SubstituteVariable(operations, i + 1, propagateEnd, dstVar, set.Src);
                    operations.RemoveAt(i);
                    i--;
                    changed = true;
                    continue;
                }

                if (dstVar.IsTemp
                    && CanPropagateToCall(operations, i, set))
                {
                    var lastReadIndex = FindLastReadIndex(operations, i, dstVar);
                    if (lastReadIndex >= 0)
                    {
                        SubstituteVariableInCallArguments(operations, i + 1, lastReadIndex, dstVar, set.Src);
                    }

                    // Не удаляем temp, если подстановка не сняла все использования (иначе temp1 в printf).
                    if (IsVariableReadAfter(operations, i, dstVar))
                    {
                        continue;
                    }

                    operations.RemoveAt(i);
                    i--;
                    changed = true;
                    continue;
                }

                if (dstVar.IsTemp
                    && i + 1 < operations.Count
                    && operations[i + 1] is IfOperation branch
                    && IsOnlyUsedInIfCondition(branch, dstVar)
                    && IsSafeToInlineIntoCall(set.Src))
                {
                    operations[i + 1] = new IfOperation(
                        ExprSubstitution.Replace(branch.Condition, dstVar, set.Src),
                        branch.ThenBody,
                        branch.ElseBody);
                    operations.RemoveAt(i);
                    i--;
                    changed = true;
                }
            }
        }

        EnsureLoopCounterIteration(operations);

        return operations;
    }

    /// <summary>
    /// Восстанавливает пропавшие инкременты счётчиков (случай fornt /Ox, где inc mem после вложенного for терялся в collect).
    /// </summary>
    private static void EnsureLoopCounterIteration(List<Operation> operations)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = EnsureForOp(operations[i]);
        }
    }

    private static Operation EnsureForOp(Operation op)
    {
        switch (op)
        {
            case WhileOperation wh when wh.Condition is CmpExpr cmp && cmp.Left is VariableExpr { Var: var cv } && cv.IsStack:
                bool hasAdvance = OperationFlattener.EnumerateNested(wh.Body).Any(o =>
                    o is IncOperation or DecOperation ||
                    (o is AddAssignOperation aa && AssignmentTarget.TryGetVariable(aa.Target, out var t) && ReferenceEquals(t, cv)) ||
                    (o is SetOperation s && AssignmentTarget.TryGetVariable(s.Dst, out var d) && ReferenceEquals(d, cv) && s.Src is Math2Expr));
                if (!hasAdvance || !(wh.Body.LastOrDefault() is IncOperation or DecOperation or AddAssignOperation))
                {
                    var newBody = new List<Operation>(wh.Body) { new IncOperation(cv.ToSet()) };
                    return new WhileOperation(wh.Condition, newBody);
                }
                return wh;

            case WhileOperation w:
                return new WhileOperation(w.Condition, w.Body.Select(EnsureForOp).ToList());

            case DoWhileOperation d:
                return new DoWhileOperation(d.Condition, d.Body.Select(EnsureForOp).ToList());

            case ForOperation f:
                return new ForOperation(
                    f.Init is not null ? EnsureForOp(f.Init) as SetOperation : null,
                    f.Condition,
                    f.Iteration is not null ? EnsureForOp(f.Iteration) : null,
                    f.Body.Select(EnsureForOp).ToList());

            case IfOperation iff:
                return new IfOperation(iff.Condition, iff.ThenBody.Select(EnsureForOp).ToList(), iff.ElseBody?.Select(EnsureForOp).ToList());

            default:
                return op;
        }
    }

    private static Operation OptimizeNested(Operation operation) =>
        operation switch
        {
            IfOperation branch => new IfOperation(
                branch.Condition,
                OptimizeList(branch.ThenBody.ToList()),
                branch.ElseBody is not null ? OptimizeList(branch.ElseBody.ToList()) : null),
            WhileOperation loop => new WhileOperation(loop.Condition, OptimizeList(loop.Body.ToList())),
            DoWhileOperation loop => new DoWhileOperation(loop.Condition, OptimizeList(loop.Body.ToList())),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? OptimizeNested(loop.Init) : null,
                loop.Condition,
                loop.Iteration is not null ? OptimizeNested(loop.Iteration) : null,
                OptimizeList(loop.Body.ToList())),
            SwitchOperation sw => OperationTreeMapper.MapSwitchBodies(
                sw,
                bodies => OptimizeList(bodies.ToList())),
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
            return true;
        }

        return !DefinesVariableBetween(operations, copyIndex, lastReadIndex, src);
    }

    private static int FindNextDefinitionIndex(IReadOnlyList<Operation> operations, int fromIndex, Variable variable)
    {
        for (var i = fromIndex + 1; i < operations.Count; i++)
        {
            if (operations[i] is SetOperation set && AssignmentTarget.DefinesVariable(set.Dst, variable))
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
                || !AssignmentTarget.TryGetVariable(candidate.Dst, out var candidateDst)
                || !ReferenceEquals(candidateDst, temp)
                || candidateDst.IsStack
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

    /// <summary>
    /// Выражения, которые можно подставить напрямую вместо пары <c>temp = expr; dst = temp</c>.
    /// Копии через другую переменную обрабатывает copy propagation; здесь — вызовы и составные выражения.
    /// </summary>
    private static bool IsFoldableTempExpression(Expr expr) =>
        expr is not (StringExpr or ImageOffsetExpr or VariableExpr);

    /// <summary>
    /// Выражение безопасно для подстановки в аргументы вызова (без дублирования side-effect и MemExpr).
    /// </summary>
    private static bool IsSafeToInlineIntoCall(Expr expr) =>
        expr switch
        {
            VariableExpr { Var: var v } => !v.IsInternal,
            ConstExpr or StringExpr => true,
            MemberExpr member => IsSafeToInlineIntoCall(member.Base),
            Math1Expr unary => IsSafeToInlineIntoCall(unary.Op),
            Math2Expr binary => IsSafeToInlineIntoCall(binary.First) && IsSafeToInlineIntoCall(binary.Second),
            CmpExpr cmp => IsSafeToInlineIntoCall(cmp.Left) && IsSafeToInlineIntoCall(cmp.Right),
            CallExpr => true,
            IncDecExpr or MemExpr or ImageOffsetExpr or AddressOfExpr => false,
            _ => false,
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
            if (ReferenceEquals(variable, dst))
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
        if (!AssignmentTarget.TryGetVariable(set.Dst, out var dstVar)
            || !IsOnlyUsedInReturn(operations, setIndex, dstVar))
        {
            return false;
        }

        var lastReadIndex = FindLastReadIndex(operations, setIndex, dstVar);
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

    /// <summary>
    /// Разрешает подставить <c>temp = expr</c> напрямую в аргументы единственного вызова.
    /// Не дублируем вызовы с побочными эффектами и сегментные обращения (dos.c и т.п.).
    /// </summary>
    private static bool IsOnlyUsedInIfCondition(IfOperation branch, Variable variable) =>
        ExprSubstitution.Contains(branch.Condition, variable)
        && !branch.ThenBody.Any(op => ReadsVariableDeep(op, variable))
        && !(branch.ElseBody?.Any(op => ReadsVariableDeep(op, variable)) ?? false);

    private static bool CanPropagateToCall(
        IReadOnlyList<Operation> operations,
        int setIndex,
        SetOperation set)
    {
        if (!AssignmentTarget.TryGetVariable(set.Dst, out var dstVar)
            || !IsSafeToInlineIntoCall(set.Src)
            || !IsOnlyUsedInCallArguments(operations, setIndex, dstVar))
        {
            return false;
        }

        var lastReadIndex = FindLastReadIndex(operations, setIndex, dstVar);
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

    private static bool IsOnlyUsedInCallArguments(
        IReadOnlyList<Operation> operations,
        int defIndex,
        Variable variable)
    {
        for (var i = defIndex + 1; i < operations.Count; i++)
        {
            if (ReadsVariableOutsideCallArguments(operations[i], variable))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ReadsVariableOutsideCallArguments(Operation operation, Variable variable) =>
        operation switch
        {
            CallOperation => false,
            SetOperation { Src: CallExpr } set => AssignmentTarget.DefinesVariable(set.Dst, variable),
            ReturnOperation => false,
            SetOperation set => AssignmentTarget.DefinesVariable(set.Dst, variable)
                || ExprSubstitution.Contains(set.Dst, variable)
                || ExprSubstitution.Contains(set.Src, variable),
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
            IfOperation branch => ExprSubstitution.Contains(branch.Condition, variable)
                || branch.ThenBody.Any(op => ReadsVariableOutsideCallArguments(op, variable))
                || (branch.ElseBody?.Any(op => ReadsVariableOutsideCallArguments(op, variable)) ?? false),
            WhileOperation loop => ExprSubstitution.Contains(loop.Condition, variable)
                || loop.Body.Any(op => ReadsVariableOutsideCallArguments(op, variable)),
            DoWhileOperation loop => ExprSubstitution.Contains(loop.Condition, variable)
                || loop.Body.Any(op => ReadsVariableOutsideCallArguments(op, variable)),
            ForOperation loop => (loop.Init is not null && ReadsVariableOutsideCallArguments(loop.Init, variable))
                || ExprSubstitution.Contains(loop.Condition, variable)
                || (loop.Iteration is not null && ReadsVariableOutsideCallArguments(loop.Iteration, variable))
                || loop.Body.Any(op => ReadsVariableOutsideCallArguments(op, variable)),
            SwitchOperation sw => OperationTreeMapper.SwitchUsesVariable(
                sw,
                variable,
                ReadsVariableOutsideCallArguments),
            _ => false,
        };

    private static void SubstituteVariableInCallArguments(
        List<Operation> operations,
        int fromIndex,
        int toInclusive,
        Variable from,
        Expr to)
    {
        for (var i = fromIndex; i <= toInclusive && i < operations.Count; i++)
        {
            operations[i] = SubstituteInCallArguments(operations[i], from, to);
        }
    }

    private static Operation SubstituteInCallArguments(Operation operation, Variable from, Expr to) =>
        operation switch
        {
            CallOperation call => new CallOperation(
                call.Name,
                call.Args.Select(arg => ExprSubstitution.Replace(arg, from, to)).ToList()),
            SetOperation { Src: CallExpr call } set => new SetOperation(
                set.Dst,
                new CallExpr(call.Name, call.Args.Select(arg => ExprSubstitution.Replace(arg, from, to)).ToList())
                {
                    CallState = call.CallState,
                }),
            IfOperation branch => new IfOperation(
                branch.Condition,
                branch.ThenBody.Select(op => SubstituteInCallArguments(op, from, to)).ToList(),
                branch.ElseBody?.Select(op => SubstituteInCallArguments(op, from, to)).ToList()),
            WhileOperation loop => new WhileOperation(
                loop.Condition,
                loop.Body.Select(op => SubstituteInCallArguments(op, from, to)).ToList()),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? SubstituteInCallArguments(loop.Init, from, to) : null,
                loop.Condition,
                loop.Iteration is not null ? SubstituteInCallArguments(loop.Iteration, from, to) : null,
                loop.Body.Select(op => SubstituteInCallArguments(op, from, to)).ToList()),
            SwitchOperation sw => OperationTreeMapper.MapSwitchBodies(
                sw,
                bodies => bodies.Select(op => SubstituteInCallArguments(op, from, to)).ToList()),
            _ => operation,
        };

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
            AddAssignOperation add => ExprSubstitution.Contains(add.Target, variable)
                || (add.Segment is not null && ExprSubstitution.Contains(add.Segment, variable))
                || ExprSubstitution.Contains(add.Value, variable),
            SubAssignOperation sub => ExprSubstitution.Contains(sub.Target, variable)
                || (sub.Segment is not null && ExprSubstitution.Contains(sub.Segment, variable))
                || ExprSubstitution.Contains(sub.Value, variable),
            IfOperation branch => ExprSubstitution.Contains(branch.Condition, variable)
                || branch.ThenBody.Any(op => ReadsVariableOutsideReturn(op, variable))
                || (branch.ElseBody?.Any(op => ReadsVariableOutsideReturn(op, variable)) ?? false),
            WhileOperation loop => ExprSubstitution.Contains(loop.Condition, variable)
                || loop.Body.Any(op => ReadsVariableOutsideReturn(op, variable)),
            DoWhileOperation loop => ExprSubstitution.Contains(loop.Condition, variable)
                || loop.Body.Any(op => ReadsVariableOutsideReturn(op, variable)),
            ForOperation loop => (loop.Init is not null && ReadsVariableOutsideReturn(loop.Init, variable))
                || ExprSubstitution.Contains(loop.Condition, variable)
                || (loop.Iteration is not null && ReadsVariableOutsideReturn(loop.Iteration, variable))
                || loop.Body.Any(op => ReadsVariableOutsideReturn(op, variable)),
            SwitchOperation sw => OperationTreeMapper.SwitchUsesVariable(
                sw,
                variable,
                ReadsVariableOutsideReturn),
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
            _ => operation,
        };

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
            SwitchOperation sw => OperationTreeMapper.SwitchUsesVariable(
                sw,
                variable,
                ReadsVariableDeep),
            _ => false,
        };

    /// <summary>
    /// <c>temp = expr; seg:[temp] = v</c> → <c>seg:[expr] = v</c>, если temp больше нигде не читается.
    /// </summary>
    private static bool TryFoldTempIntoStoreAddress(List<Operation> operations, int tempAssignIndex)
    {
        if (tempAssignIndex + 1 >= operations.Count
            || operations[tempAssignIndex] is not SetOperation { Dst: VariableExpr { Var: var temp }, Src: var addressExpr }
            || !temp.IsTemp
            || operations[tempAssignIndex + 1] is not StoreOperation store
            || !AssignmentTarget.ReferencesVariable(store.Address, temp))
        {
            return false;
        }

        if (FindLastReadIndexInRange(operations, tempAssignIndex, tempAssignIndex + 2, temp) != tempAssignIndex + 1)
        {
            return false;
        }

        operations[tempAssignIndex + 1] = store with { Address = addressExpr };
        operations.RemoveAt(tempAssignIndex);
        return true;
    }

    /// <summary>
    /// Подставляет <c>temp = expr</c> в следующую операцию (Store или составное +=/-=), если temp больше нигде не используется.
    /// </summary>
    private static bool TrySubstituteTempInNextOperation(List<Operation> operations, int tempAssignIndex)
    {
        if (tempAssignIndex + 1 >= operations.Count
            || operations[tempAssignIndex] is not SetOperation { Dst: VariableExpr { Var: var temp }, Src: var expr }
            || !temp.IsTemp)
        {
            return false;
        }

        var next = operations[tempAssignIndex + 1];
        if (next is not (StoreOperation or AddAssignOperation or SubAssignOperation))
        {
            return false;
        }

        if (FindLastReadIndexInRange(operations, tempAssignIndex, tempAssignIndex + 2, temp) != tempAssignIndex + 1)
        {
            return false;
        }

        operations[tempAssignIndex + 1] = SubstituteVariableDeep(next, temp, expr);
        operations.RemoveAt(tempAssignIndex);
        return true;
    }

    private static Operation SubstituteVariableDeep(Operation operation, Variable from, Expr to) =>
        operation switch
        {
            SetOperation set => set with
            {
                Dst = ExprSubstitution.Replace(set.Dst, from, to),
                Src = ExprSubstitution.Replace(set.Src, from, to),
            },
            StoreOperation store => store with
            {
                Address = ExprSubstitution.Replace(store.Address, from, to),
                Segment = store.Segment is null ? null : ExprSubstitution.Replace(store.Segment, from, to),
                Value = ExprSubstitution.Replace(store.Value, from, to),
            },
            IncOperation inc => inc with
            {
                Target = ReplaceIncDecTarget(inc.Target, from, to),
                Segment = inc.Segment is null ? null : ExprSubstitution.Replace(inc.Segment, from, to),
            },
            DecOperation dec => dec with
            {
                Target = ReplaceIncDecTarget(dec.Target, from, to),
                Segment = dec.Segment is null ? null : ExprSubstitution.Replace(dec.Segment, from, to),
            },
            AddAssignOperation add => add with
            {
                Target = ExprSubstitution.Replace(add.Target, from, to),
                Value = ExprSubstitution.Replace(add.Value, from, to),
                Segment = add.Segment is null ? null : ExprSubstitution.Replace(add.Segment, from, to),
            },
            SubAssignOperation sub => sub with
            {
                Target = ExprSubstitution.Replace(sub.Target, from, to),
                Value = ExprSubstitution.Replace(sub.Value, from, to),
                Segment = sub.Segment is null ? null : ExprSubstitution.Replace(sub.Segment, from, to),
            },
            IfOperation branch => branch with
            {
                Condition = ExprSubstitution.Replace(branch.Condition, from, to),
                ThenBody = branch.ThenBody.Select(op => SubstituteVariableDeep(op, from, to)).ToList(),
                ElseBody = branch.ElseBody?.Select(op => SubstituteVariableDeep(op, from, to)).ToList(),
            },
            WhileOperation loop => loop with
            {
                Condition = ExprSubstitution.Replace(loop.Condition, from, to),
                Body = loop.Body.Select(op => SubstituteVariableDeep(op, from, to)).ToList(),
            },
            DoWhileOperation loop => loop with
            {
                Condition = ExprSubstitution.Replace(loop.Condition, from, to),
                Body = loop.Body.Select(op => SubstituteVariableDeep(op, from, to)).ToList(),
            },
            ForOperation loop => loop with
            {
                Init = loop.Init is null ? null : SubstituteVariableDeep(loop.Init, from, to),
                Condition = loop.Condition is null ? null : ExprSubstitution.Replace(loop.Condition, from, to),
                Iteration = loop.Iteration is null ? null : SubstituteVariableDeep(loop.Iteration, from, to),
                Body = loop.Body.Select(op => SubstituteVariableDeep(op, from, to)).ToList(),
            },
            SwitchOperation sw => new SwitchOperation(
                ExprSubstitution.Replace(sw.Discriminant, from, to),
                sw.Cases.Select(c => new SwitchCase(
                    c.Value,
                    c.Body.Select(op => SubstituteVariableDeep(op, from, to)).ToList())).ToList()),
            _ => operation,
        };

    /// <summary>
    /// <c>temp = local ± K; local = temp</c> → <c>local = local ± K</c> (mov/add/mov из QuickC).
    /// </summary>
    private static bool TryFoldSelfAssignTempStore(List<Operation> operations, int assignIndex)
    {
        if (assignIndex <= 0
            || operations[assignIndex] is not SetOperation { Src: VariableExpr { Var: var temp }, Dst: VariableExpr { Var: var dst } }
            || operations[assignIndex - 1] is not SetOperation { Dst: VariableExpr { Var: var tempDef }, Src: Math2Expr math }
            || !ReferenceEquals(tempDef, temp)
            || math.First is not VariableExpr { Var: var local }
            || !ReferenceEquals(local, dst))
        {
            return false;
        }

        Expr folded;
        if (WordArithmeticHelper.TryNormalizeSelfAssignMath(local, math, isWord: true, out var normalized))
        {
            folded = normalized;
        }
        else if (math.Operation is Math2Operation.Add or Math2Operation.Sub)
        {
            folded = math;
        }
        else
        {
            return false;
        }

        operations[assignIndex - 1] = new SetOperation(dst.ToSet(), folded);
        operations.RemoveAt(assignIndex);
        return true;
    }

    private static Expr ReplaceIncDecTarget(Expr target, Variable from, Expr to)
    {
        if (target is VariableExpr { Var: var variable } && ReferenceEquals(variable, from))
        {
            return target;
        }

        return ExprSubstitution.Replace(target, from, to);
    }

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
            IncOperation inc when AssignmentTarget.TryGetVariable(inc.Target, out var target) => ReferenceEquals(target, variable),
            DecOperation dec when AssignmentTarget.TryGetVariable(dec.Target, out var target) => ReferenceEquals(target, variable),
            AddAssignOperation add => AssignmentTarget.DefinesVariable(add.Target, variable),
            SubAssignOperation sub => AssignmentTarget.DefinesVariable(sub.Target, variable),
            _ => false,
        };
}

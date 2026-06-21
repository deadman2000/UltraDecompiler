namespace UltraDecompiler.CodeGeneration.Rendering;

/// <summary>
/// Собирает переменные, встречающиеся в IR-операциях процедуры (для объявлений в C).
/// </summary>
public static class UsedVariableCollector
{
    private static readonly IEqualityComparer<Variable> ByReference = ReferenceEqualityComparer.Instance;

    /// <summary>Собирает все переменные, упомянутые в операциях (включая параметры в условиях).</summary>
    public static IReadOnlyCollection<Variable> CollectReferenced(IEnumerable<Operation> operations)
    {
        var result = new Dictionary<Variable, Variable>(ByReference);
        foreach (var op in OperationFlattener.EnumerateNested(operations))
        {
            AddVariablesFromOperation(op, result);
        }

        return result.Keys;
    }

    /// <summary>
    /// Возвращает локальные переменные процедуры для объявления в C.
    /// Параметры функции (<paramref name="parameterVariables"/>) исключаются.
    /// Стековые локали включаются только если они упомянуты в IR или требуют объявления
    /// (far-указатель с инициализатором, массив на стеке).
    /// </summary>
    public static IReadOnlyList<Variable> Collect(
        IEnumerable<Operation> operations,
        IEnumerable<Variable> parameterVariables,
        IEnumerable<Variable>? stackVariables = null)
    {
        var exclude = new HashSet<Variable>(parameterVariables, ByReference);
        var result = new Dictionary<Variable, Variable>(ByReference);

        foreach (var op in OperationFlattener.EnumerateNested(operations))
        {
            AddVariablesFromOperation(op, result);
        }

        if (stackVariables is not null)
        {
            foreach (var stackVar in stackVariables)
            {
                if (exclude.Contains(stackVar) || !stackVar.RequiresCDeclaration)
                {
                    continue;
                }

                if (stackVar.FarPointerInitializer is not null
                    || stackVar.ArraySize is not null
                    || result.ContainsKey(stackVar))
                {
                    AddVariable(stackVar, result);
                }
            }
        }

        return result.Values
            .Where(v => !exclude.Contains(v) && v.RequiresCDeclaration)
            .OrderBy(static v => v.IsStack ? 0 : 1)
            .ThenBy(static v => v.Number)
            .ToList();
    }

    private static void AddVariablesFromOperation(Operation op, Dictionary<Variable, Variable> result)
    {
        switch (op)
        {
            case SetOperation set:
                AddFromExpr(set.Dst, result);
                AddFromExpr(set.Src, result);
                break;
            case StoreOperation store:
                AddFromExpr(store.Address, result);
                AddFromExpr(store.Segment, result);
                AddFromExpr(store.Value, result);
                break;
            case IncOperation inc:
                AddFromExpr(inc.Target, result);
                AddFromExpr(inc.Segment, result);
                break;
            case AddAssignOperation add:
                AddFromExpr(add.Target, result);
                AddFromExpr(add.Segment, result);
                AddFromExpr(add.Value, result);
                break;
            case SubAssignOperation sub:
                AddFromExpr(sub.Target, result);
                AddFromExpr(sub.Segment, result);
                AddFromExpr(sub.Value, result);
                break;
            case DecOperation dec:
                AddFromExpr(dec.Target, result);
                AddFromExpr(dec.Segment, result);
                break;
            case CallOperation call:
                foreach (var arg in call.Args)
                {
                    AddFromExpr(arg, result);
                }
                break;
            case ReturnOperation ret:
                AddFromExpr(ret.Value, result);
                break;
            case WhileOperation loop:
                AddFromExpr(loop.Condition, result);
                break;
            case DoWhileOperation loop:
                AddFromExpr(loop.Condition, result);
                break;
            case ForOperation loop:
                AddFromExpr(loop.Condition, result);
                if (loop.Init != null)
                {
                    AddVariablesFromOperation(loop.Init, result);
                }
                if (loop.Iteration != null)
                {
                    AddVariablesFromOperation(loop.Iteration, result);
                }
                break;
            case IfOperation branch:
                AddFromExpr(branch.Condition, result);
                break;
            case SwitchOperation sw:
                AddFromExpr(sw.Discriminant, result);
                break;
        }
    }

    private static void AddFromExpr(Expr? expr, Dictionary<Variable, Variable> result)
    {
        if (expr is null)
        {
            return;
        }

        if (expr is VariableExpr { Var: var variable })
        {
            AddVariable(variable, result);
            return;
        }

        switch (expr)
        {
            case MemberExpr member:
                AddFromExpr(member.Base, result);
                break;
            case AddressOfExpr addr:
                AddFromExpr(addr.Operand, result);
                break;
            case Math1Expr m1:
                AddFromExpr(m1.Op, result);
                break;
            case Math2Expr m2:
                AddFromExpr(m2.First, result);
                AddFromExpr(m2.Second, result);
                break;
            case MemExpr mem:
                AddFromExpr(mem.Address, result);
                AddFromExpr(mem.Segment, result);
                break;
            case CmpExpr cmp:
                AddFromExpr(cmp.Left, result);
                AddFromExpr(cmp.Right, result);
                break;
            case CallExpr call:
                foreach (var arg in call.Args)
                {
                    AddFromExpr(arg, result);
                }
                break;
            case SyntheticLoadExpr synthetic:
                if (synthetic.Array is not null)
                {
                    AddVariable(synthetic.Array, result);
                }

                if (synthetic.Index is not null)
                {
                    AddVariable(synthetic.Index, result);
                }

                break;
        }
    }

    private static void AddVariable(Variable variable, Dictionary<Variable, Variable> result)
    {
        if (variable.IsRegister)
        {
            throw new InvalidOperationException("Регистровые переменные должны исчезнуть с оптимизацией");
        }

        result.TryAdd(variable, variable);
    }
}

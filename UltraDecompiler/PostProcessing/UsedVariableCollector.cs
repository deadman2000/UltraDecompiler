using UltraDecompiler.Decompilation;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Собирает переменные, встречающиеся в IR-операциях процедуры (для объявлений в C).
/// </summary>
internal static class UsedVariableCollector
{
    /// <summary>
    /// Возвращает локальные переменные процедуры в порядке создания (<see cref="Variable.Number"/>).
    /// Параметры функции (<paramref name="parameterVariables"/>) исключаются.
    /// </summary>
    public static IReadOnlyList<Variable> Collect(
        IEnumerable<Operation> operations,
        IEnumerable<Variable> parameterVariables)
    {
        var exclude = new HashSet<int>(parameterVariables.Select(static v => v.Number));
        var result = new Dictionary<int, Variable>();

        foreach (var op in ExpressionBuilder.EnumerateNested(operations))
        {
            AddVariablesFromOperation(op, result);
        }

        return result.Values
            .Where(v => !exclude.Contains(v.Number) && !IsImplicitSegmentVariable(v))
            .OrderBy(static v => v.Number)
            .ToList();
    }

    private static void AddVariablesFromOperation(Operation op, Dictionary<int, Variable> result)
    {
        switch (op)
        {
            case SetOperation set:
                AddVariable(set.Dst, result);
                AddFromExpr(set.Src, result);
                break;
            case StoreOperation store:
                AddFromExpr(store.Address, result);
                AddFromExpr(store.Segment, result);
                AddFromExpr(store.Value, result);
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
        }
    }

    private static void AddFromExpr(Expr? expr, Dictionary<int, Variable> result)
    {
        if (expr is null)
        {
            return;
        }

        if (expr is Variable variable)
        {
            AddVariable(variable, result);
            return;
        }

        switch (expr)
        {
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
        }
    }

    private static void AddVariable(Variable variable, Dictionary<int, Variable> result) =>
        result.TryAdd(variable.Number, variable);

    private static bool IsImplicitSegmentVariable(Variable variable) =>
        variable.Name is "_psp";
}

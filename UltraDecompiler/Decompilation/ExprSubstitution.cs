namespace UltraDecompiler.Decompilation;

/// <summary>
/// Подстановка переменных в деревьях <see cref="Expr"/>.
/// </summary>
internal static class ExprSubstitution
{
    /// <summary>
    /// Заменяет все вхождения <paramref name="from"/> на <paramref name="to"/>.
    /// </summary>
    public static Expr Replace(Expr expr, Variable from, Expr to)
    {
        if (expr is Variable v && v.Number == from.Number)
        {
            return to;
        }

        return expr switch
        {
            ConstExpr or StringExpr or ImageOffsetExpr => expr,
            Variable => expr,
            Math1Expr m => m with { Op = Replace(m.Op, from, to) },
            Math2Expr m => m with
            {
                First = Replace(m.First, from, to),
                Second = Replace(m.Second, from, to),
            },
            MemExpr mem => mem with
            {
                Address = Replace(mem.Address, from, to),
                Segment = mem.Segment is null ? null : Replace(mem.Segment, from, to),
            },
            CmpExpr cmp => cmp with
            {
                Left = Replace(cmp.Left, from, to),
                Right = Replace(cmp.Right, from, to),
            },
            CallExpr call => call with
            {
                Args = call.Args.Select(arg => Replace(arg, from, to)).ToList(),
            },
            _ => expr,
        };
    }

    /// <summary>
    /// Собирает все переменные, встречающиеся в выражении.
    /// </summary>
    public static HashSet<Variable> CollectVariables(Expr? expr)
    {
        var result = new HashSet<Variable>();
        CollectVariablesRecursive(expr, result);
        return result;
    }

    private static void CollectVariablesRecursive(Expr? expr, HashSet<Variable> result)
    {
        if (expr is null)
        {
            return;
        }

        if (expr is Variable variable)
        {
            result.Add(variable);
            return;
        }

        switch (expr)
        {
            case Math1Expr m:
                CollectVariablesRecursive(m.Op, result);
                break;
            case Math2Expr m:
                CollectVariablesRecursive(m.First, result);
                CollectVariablesRecursive(m.Second, result);
                break;
            case MemExpr mem:
                CollectVariablesRecursive(mem.Address, result);
                CollectVariablesRecursive(mem.Segment, result);
                break;
            case CmpExpr cmp:
                CollectVariablesRecursive(cmp.Left, result);
                CollectVariablesRecursive(cmp.Right, result);
                break;
            case CallExpr call:
                foreach (var arg in call.Args)
                {
                    CollectVariablesRecursive(arg, result);
                }

                break;
        }
    }

    /// <summary>
    /// Проверяет, встречается ли переменная в выражении.
    /// </summary>
    public static bool Contains(Expr? expr, Variable variable)
    {
        if (expr is null)
        {
            return false;
        }

        if (expr is Variable v && v.Number == variable.Number)
        {
            return true;
        }

        return expr switch
        {
            ConstExpr or StringExpr or ImageOffsetExpr => false,
            Math1Expr m => Contains(m.Op, variable),
            Math2Expr m => Contains(m.First, variable) || Contains(m.Second, variable),
            MemExpr mem => Contains(mem.Address, variable) || Contains(mem.Segment, variable),
            CmpExpr cmp => Contains(cmp.Left, variable) || Contains(cmp.Right, variable),
            CallExpr call => call.Args.Any(arg => Contains(arg, variable)),
            _ => false,
        };
    }
}

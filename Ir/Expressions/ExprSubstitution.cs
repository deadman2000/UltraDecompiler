using UltraDecompiler.Decompilation;

namespace UltraDecompiler.Ir.Expressions;

/// <summary>
/// Подстановка переменных в деревьях <see cref="Expr"/>.
/// </summary>
public static class ExprSubstitution
{
    /// <summary>
    /// Заменяет все вхождения <paramref name="from"/> на <paramref name="to"/>.
    /// </summary>
    public static Expr Replace(Expr expr, Variable from, Expr to)
    {
        if (expr is Variable v && ReferenceEquals(v, from))
        {
            return to;
        }

        return expr switch
        {
            ConstExpr or CharConstExpr or StringExpr or ImageOffsetExpr => expr,
            Variable => expr,
            MemberExpr member => member with { Base = Replace(member.Base, from, to) },
            IncDecExpr inc => inc with { Operand = Replace(inc.Operand, from, to) },
            AddressOfExpr addr => addr with { Operand = Replace(addr.Operand, from, to) },
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
            LongExpr longExpr => longExpr with
            {
                Low = Replace(longExpr.Low, from, to),
                High = Replace(longExpr.High, from, to),
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
            case MemberExpr member:
                CollectVariablesRecursive(member.Base, result);
                break;
            case AddressOfExpr addr:
                CollectVariablesRecursive(addr.Operand, result);
                break;
            case IncDecExpr inc:
                CollectVariablesRecursive(inc.Operand, result);
                break;
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
            case LongExpr longExpr:
                CollectVariablesRecursive(longExpr.Low, result);
                CollectVariablesRecursive(longExpr.High, result);
                break;
        }
    }

    /// <summary>
    /// Собирает все обращения к памяти (<see cref="MemExpr"/>) в выражении.
    /// </summary>
    public static HashSet<MemExpr> CollectMemExprs(Expr? expr)
    {
        var result = new HashSet<MemExpr>();
        CollectMemExprsRecursive(expr, result);
        return result;
    }

    private static void CollectMemExprsRecursive(Expr? expr, HashSet<MemExpr> result)
    {
        if (expr is null)
        {
            return;
        }

        if (expr is MemExpr mem)
        {
            result.Add(mem);
            CollectMemExprsRecursive(mem.Address, result);
            CollectMemExprsRecursive(mem.Segment, result);
            return;
        }

        switch (expr)
        {
            case MemberExpr member:
                CollectMemExprsRecursive(member.Base, result);
                break;
            case AddressOfExpr addr:
                CollectMemExprsRecursive(addr.Operand, result);
                break;
            case IncDecExpr inc:
                CollectMemExprsRecursive(inc.Operand, result);
                break;
            case Math1Expr m:
                CollectMemExprsRecursive(m.Op, result);
                break;
            case Math2Expr m:
                CollectMemExprsRecursive(m.First, result);
                CollectMemExprsRecursive(m.Second, result);
                break;
            case CmpExpr cmp:
                CollectMemExprsRecursive(cmp.Left, result);
                CollectMemExprsRecursive(cmp.Right, result);
                break;
            case CallExpr call:
                foreach (var arg in call.Args)
                {
                    CollectMemExprsRecursive(arg, result);
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

        if (expr is Variable v && ReferenceEquals(v, variable))
        {
            return true;
        }

        return expr switch
        {
            ConstExpr or CharConstExpr or StringExpr or ImageOffsetExpr => false,
            MemberExpr member => Contains(member.Base, variable),
            AddressOfExpr addr => Contains(addr.Operand, variable),
            IncDecExpr inc => Contains(inc.Operand, variable),
            Math1Expr m => Contains(m.Op, variable),
            Math2Expr m => Contains(m.First, variable) || Contains(m.Second, variable),
            MemExpr mem => Contains(mem.Address, variable) || Contains(mem.Segment, variable),
            CmpExpr cmp => Contains(cmp.Left, variable) || Contains(cmp.Right, variable),
            CallExpr call => call.Args.Any(arg => Contains(arg, variable)),
            _ => false,
        };
    }
}

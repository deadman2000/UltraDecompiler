using UltraDecompiler.Decompilation;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Убирает лишние <c>&amp; 255</c> в счётчиках сдвигов: QuickC в исходнике пишет <c>n</c>,
/// а не <c>n &amp; 255</c>, хотя в машинном коде сдвиг идёт через CL.
/// </summary>
public static class ShiftCountSimplifier
{
    /// <summary>
    /// Упрощает выражения-счётчики сдвигов во всех операциях процедуры.
    /// </summary>
    public static IReadOnlyList<Operation> Simplify(IReadOnlyList<Operation> operations) =>
        operations.Select(SimplifyOperation).ToList();

    private static Operation SimplifyOperation(Operation op) =>
        op switch
        {
            SetOperation set => set with { Src = SimplifyExpr(set.Src), Dst = SimplifyExpr(set.Dst) },
            StoreOperation store => store with
            {
                Address = SimplifyExpr(store.Address),
                Segment = SimplifyExpr(store.Segment),
                Value = SimplifyExpr(store.Value),
            },
            ReturnOperation ret => ret with { Value = SimplifyExpr(ret.Value) },
            CallOperation call => call with { Args = call.Args.Select(SimplifyExpr).ToList() },
            IfOperation branch => branch with
            {
                Condition = SimplifyExpr(branch.Condition),
                ThenBody = branch.ThenBody.Select(SimplifyOperation).ToList(),
                ElseBody = branch.ElseBody?.Select(SimplifyOperation).ToList(),
            },
            WhileOperation loop => loop with
            {
                Condition = SimplifyExpr(loop.Condition),
                Body = loop.Body.Select(SimplifyOperation).ToList(),
            },
            ForOperation loop => loop with
            {
                Init = loop.Init is null ? null : SimplifyOperation(loop.Init),
                Condition = SimplifyExpr(loop.Condition),
                Iteration = loop.Iteration is null ? null : SimplifyOperation(loop.Iteration),
                Body = loop.Body.Select(SimplifyOperation).ToList(),
            },
            _ => op,
        };

    private static Expr SimplifyExpr(Expr? expr)
    {
        if (expr is null)
        {
            return ConstExpr.Zero;
        }

        return expr switch
        {
            Math2Expr { Operation: Math2Operation.Shl or Math2Operation.Shr, First: var value, Second: var count } shift
                => shift with
                {
                    First = SimplifyExpr(value),
                    Second = SimplifyShiftCount(count),
                },
            Math1Expr m => m with { Op = SimplifyExpr(m.Op) },
            Math2Expr m => m with
            {
                First = SimplifyExpr(m.First),
                Second = SimplifyExpr(m.Second),
            },
            CmpExpr cmp => cmp with
            {
                Left = SimplifyExpr(cmp.Left),
                Right = SimplifyExpr(cmp.Right),
            },
            MemExpr mem => mem with
            {
                Address = SimplifyExpr(mem.Address),
                Segment = mem.Segment is null ? null : SimplifyExpr(mem.Segment),
            },
            CallExpr call => call with { Args = call.Args.Select(SimplifyExpr).ToList() },
            LongExpr longExpr => longExpr with
            {
                Low = SimplifyExpr(longExpr.Low),
                High = SimplifyExpr(longExpr.High),
            },
            MemberExpr member => member with { Base = SimplifyExpr(member.Base) },
            IncDecExpr inc => inc with { Operand = SimplifyExpr(inc.Operand) },
            AddressOfExpr addr => addr with { Operand = SimplifyExpr(addr.Operand) },
            _ => expr,
        };
    }

    private static Expr SimplifyShiftCount(Expr count)
    {
        var simplified = SimplifyExpr(count);

        if (simplified is Math2Expr
            {
                Operation: Math2Operation.And,
                First: var masked,
                Second: ConstExpr { Value: 255 or 0xFF },
            }
            && !ContainsCharTypedVariable(masked))
        {
            return masked;
        }

        return simplified;
    }

    private static bool ContainsCharTypedVariable(Expr expr)
    {
        foreach (var variable in ExprSubstitution.CollectVariables(expr))
        {
            if (variable.Type?.Kind == CTypeKind.Char)
            {
                return true;
            }
        }

        return false;
    }
}

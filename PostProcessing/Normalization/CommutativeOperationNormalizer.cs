namespace UltraDecompiler.PostProcessing.Normalization;

/// <summary>
/// Восстанавливает привычный для C порядок операндов коммутативных операций.
/// IMUL задаёт <c>AX * rm</c>, поэтому после рекурсивного вызова IR часто имеет вид
/// <c>call(...) * arg</c>, тогда как QuickC в исходнике пишет <c>arg * call(...)</c>.
/// </summary>
public static class CommutativeOperationNormalizer
{
    /// <summary>Нормализует дерево операций перед генерацией C-кода.</summary>
    public static IReadOnlyList<Operation> Normalize(IReadOnlyList<Operation> operations) =>
        NormalizeList(operations.ToList());

    private static List<Operation> NormalizeList(List<Operation> operations)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = NormalizeNested(operations[i]);
        }

        return operations;
    }

    private static Operation NormalizeNested(Operation operation) =>
        operation switch
        {
            SetOperation set => new SetOperation(set.Dst, NormalizeExpr(set.Src)),
            StoreOperation store => new StoreOperation(
                NormalizeExpr(store.Address),
                store.Segment is null ? null : NormalizeExpr(store.Segment),
                NormalizeExpr(store.Value)),
            IncOperation inc => new IncOperation(
                NormalizeExpr(inc.Target),
                inc.Segment is null ? null : NormalizeExpr(inc.Segment)),
            DecOperation dec => new DecOperation(
                NormalizeExpr(dec.Target),
                dec.Segment is null ? null : NormalizeExpr(dec.Segment)),
            CallOperation call => new CallOperation(
                call.Name,
                call.Args.Select(NormalizeExpr).ToList()),
            ReturnOperation ret => new ReturnOperation(
                ret.Value is null ? null : NormalizeExpr(ret.Value),
                ret.IsExplicit),
            IfOperation branch => new IfOperation(
                NormalizeExpr(branch.Condition),
                NormalizeList(branch.ThenBody.ToList()),
                branch.ElseBody is not null ? NormalizeList(branch.ElseBody.ToList()) : null),
            WhileOperation loop => new WhileOperation(
                NormalizeExpr(loop.Condition),
                NormalizeList(loop.Body.ToList())),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? NormalizeNested(loop.Init) : null,
                loop.Condition is null ? null : NormalizeExpr(loop.Condition),
                loop.Iteration is not null ? NormalizeNested(loop.Iteration) : null,
                NormalizeList(loop.Body.ToList())),
            _ => operation,
        };

    private static Expr NormalizeExpr(Expr expr)
    {
        expr = expr switch
        {
            Math1Expr m1 => m1 with { Op = NormalizeExpr(m1.Op) },
            Math2Expr m2 => NormalizeBinary(m2),
            MemExpr mem => mem with
            {
                Address = NormalizeExpr(mem.Address),
                Segment = mem.Segment is null ? null : NormalizeExpr(mem.Segment),
            },
            CmpExpr cmp => cmp with
            {
                Left = NormalizeExpr(cmp.Left),
                Right = NormalizeExpr(cmp.Right),
            },
            CallExpr call => call with
            {
                Args = call.Args.Select(NormalizeExpr).ToList(),
            },
            AddressOfExpr addr => addr with { Operand = NormalizeExpr(addr.Operand) },
            MemberExpr member => member with { Base = NormalizeExpr(member.Base) },
            _ => expr,
        };

        return expr;
    }

    private static Math2Expr NormalizeBinary(Math2Expr binary)
    {
        var first = NormalizeExpr(binary.First);
        var second = NormalizeExpr(binary.Second);

        if (binary.Operation == Math2Operation.Mul
            && ContainsCall(first)
            && !ContainsCall(second))
        {
            return new Math2Expr(Math2Operation.Mul, second, first);
        }

        return binary with { First = first, Second = second };
    }

    private static bool ContainsCall(Expr expr) =>
        expr switch
        {
            CallExpr => true,
            Math1Expr m1 => ContainsCall(m1.Op),
            Math2Expr m2 => ContainsCall(m2.First) || ContainsCall(m2.Second),
            MemExpr mem => ContainsCall(mem.Address)
                || (mem.Segment is not null && ContainsCall(mem.Segment)),
            CmpExpr cmp => ContainsCall(cmp.Left) || ContainsCall(cmp.Right),
            AddressOfExpr addr => ContainsCall(addr.Operand),
            MemberExpr member => ContainsCall(member.Base),
            _ => false,
        };
}

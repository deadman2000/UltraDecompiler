using UltraDecompiler.Decompilation;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Упрощает тело цикла копирования строк: <c>*arg0++ = *arg1++</c>.
/// </summary>
public static class PointerLoopBodySimplifier
{
    /// <summary>
    /// Заменяет паттерн «сохранить arg+1, записать * (arg+1)» на инкремент указателей.
    /// </summary>
    public static IReadOnlyList<Operation> Simplify(IReadOnlyList<Operation> operations) =>
        SimplifyList(operations.ToList());

    private static List<Operation> SimplifyList(List<Operation> operations)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = SimplifyNested(operations[i]);
        }

        return operations;
    }

    private static Operation SimplifyNested(Operation operation) =>
        operation switch
        {
            WhileOperation loop => new WhileOperation(
                loop.Condition,
                SimplifyLoopBody(loop.Body)),
            IfOperation branch => new IfOperation(
                branch.Condition,
                SimplifyList(branch.ThenBody.ToList()),
                branch.ElseBody is not null ? SimplifyList(branch.ElseBody.ToList()) : null),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? SimplifyNested(loop.Init) : null,
                loop.Condition,
                loop.Iteration is not null ? SimplifyNested(loop.Iteration) : null,
                SimplifyList(loop.Body.ToList())),
            _ => operation,
        };

    private static IReadOnlyList<Operation> SimplifyLoopBody(IReadOnlyList<Operation> body)
    {
        var list = body.ToList();

        if (list.Count == 3
            && list[0] is SetOperation setSrc
            && list[1] is SetOperation setDst
            && list[2] is StoreOperation store
            && TryMatchPointerIncrement(setSrc, setDst, store, out var srcPtr, out var dstPtr))
        {
            return
            [
                new StoreOperation(dstPtr, store.Segment, new MemExpr(srcPtr, store.Segment)),
                new SetOperation(dstPtr, new Math2Expr(Math2Operation.Add, dstPtr, ConstExpr.One)),
                new SetOperation(srcPtr, new Math2Expr(Math2Operation.Add, srcPtr, ConstExpr.One)),
            ];
        }

        return SimplifyList(list);
    }

    private static bool TryMatchPointerIncrement(
        SetOperation setSrc,
        SetOperation setDst,
        StoreOperation store,
        out Variable srcPtr,
        out Variable dstPtr)
    {
        srcPtr = null!;
        dstPtr = null!;

        if (setSrc.Src is not Math2Expr { Operation: Math2Operation.Add, First: Variable src, Second: ConstExpr { Value: 1 } })
        {
            return false;
        }

        if (setDst.Src is not Math2Expr { Operation: Math2Operation.Add, First: Variable dst, Second: ConstExpr { Value: 1 } })
        {
            return false;
        }

        if (store.Value is not MemExpr load || !ReferenceEquals(load.Address, setSrc.Dst))
        {
            return false;
        }

        if (store.Address is not Variable storeBase || !ReferenceEquals(storeBase, setDst.Dst))
        {
            return false;
        }

        if (src.Type?.IsCharPtr != true || dst.Type?.IsCharPtr != true)
        {
            return false;
        }

        srcPtr = src;
        dstPtr = dst;
        return true;
    }
}

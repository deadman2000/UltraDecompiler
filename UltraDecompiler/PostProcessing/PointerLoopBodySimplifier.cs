using UltraDecompiler.Decompilation;
using UltraDecompiler.Decompilation.Operations;

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
            return BuildPointerCopyStep(dstPtr, srcPtr, store.Segment);
        }

        // QuickC для *dst++ = *src++: inc параметров до store с сохранённым значением в BX.
        if (list.Count == 3
            && list[0] is IncOperation
            && list[1] is IncOperation
            && list[2] is StoreOperation incStore
            && TryMatchIncrementThenStore(list[0], list[1], incStore, out srcPtr, out dstPtr))
        {
            return
            [
                new SetOperation(
                    CreatePostIncrementDeref(dstPtr, incStore.Segment),
                    CreatePostIncrementDeref(srcPtr, incStore.Segment)),
            ];
        }

        return SimplifyList(list);
    }

    private static IReadOnlyList<Operation> BuildPointerCopyStep(Variable dstPtr, Variable srcPtr, Expr? segment) =>
    [
        new SetOperation(new MemExpr(dstPtr, segment), new MemExpr(srcPtr, segment)),
        new IncOperation(dstPtr),
        new IncOperation(srcPtr),
    ];

    private static MemExpr CreatePostIncrementDeref(Variable pointer, Expr? segment) =>
        new(new IncDecExpr(IncDecKind.PostInc, pointer), segment);

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

    /// <summary>
    /// Паттерн post-increment из <c>*dst++ = *src++</c>: сначала inc указателей, затем store по уже сдвинутым адресам.
    /// </summary>
    private static bool TryMatchIncrementThenStore(
        Operation first,
        Operation second,
        StoreOperation store,
        out Variable srcPtr,
        out Variable dstPtr)
    {
        srcPtr = null!;
        dstPtr = null!;

        if (first is not IncOperation inc0 || second is not IncOperation inc1)
        {
            return false;
        }

        if (inc0.Target is not Variable var0 || inc1.Target is not Variable var1)
        {
            return false;
        }

        if (var0.Type?.IsCharPtr != true || var1.Type?.IsCharPtr != true)
        {
            return false;
        }

        if (!TryGetStorePointerTargets(store, out dstPtr, out srcPtr))
        {
            return false;
        }

        return (ReferenceEquals(dstPtr, var0) && ReferenceEquals(srcPtr, var1))
            || (ReferenceEquals(dstPtr, var1) && ReferenceEquals(srcPtr, var0));
    }

    private static bool TryGetStorePointerTargets(
        StoreOperation store,
        out Variable dstPtr,
        out Variable srcPtr)
    {
        dstPtr = null!;
        srcPtr = null!;

        if (store.Value is not MemExpr load
            || !PointerDerefFormatter.TryGetNearPointerBase(load, out srcPtr))
        {
            return false;
        }

        if (store.Address is Variable addressVar && store.Segment is null)
        {
            dstPtr = addressVar;
            return dstPtr.Type?.IsCharPtr == true;
        }

        return PointerStoreFormatter.TryGetIndexedPointer(store, out dstPtr, out var index)
            && index == 0
            && dstPtr.Type?.IsCharPtr == true;
    }
}

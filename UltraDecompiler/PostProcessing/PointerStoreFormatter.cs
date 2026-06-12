using UltraDecompiler.Decompilation;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Форматирует запись по near-указателю как <c>ptr[index] = value</c> вместо <c>seg:[ptr + index]</c>.
/// </summary>
public static class PointerStoreFormatter
{
    /// <summary>
    /// Пытается сформировать lvalue вида <c>varN[index]</c> для записи через указатель.
    /// </summary>
    public static bool TryFormat(StoreOperation store, out string lvalue)
    {
        if (FarPointerFormatter.TryFormatStore(store, out lvalue))
        {
            return true;
        }

        if (!TryGetIndexedPointer(store, out var ptr, out var index))
        {
            lvalue = string.Empty;
            return false;
        }

        lvalue = index == 0 && ptr.Type?.IsCharPtr == true
            ? $"*{ptr}"
            : index == 0
                ? $"{ptr}[0]"
                : $"{ptr}[{index}]";
        return true;
    }

    /// <summary>
    /// Возвращает базовый указатель и индекс для записи через near-указатель (<c>ptr[index]</c>).
    /// </summary>
    public static bool TryGetIndexedPointer(StoreOperation store, out Variable ptr, out int index)
    {
        ptr = null!;
        index = 0;

        // Near-указатель в small-модели: сегмент (DS/_psp) + смещение в регистре-указателе.
        if (store.Segment is null || !PointerDerefFormatter.IsNearDataSegment(store.Segment))
        {
            return false;
        }

        switch (store.Address)
        {
            case Variable basePtr when !IsSegmentBase(basePtr):
                ptr = basePtr;
                return true;

            case Math2Expr { Operation: Math2Operation.Add } add
                when TryExtractPointerIndex(add, out var indexedPtr, out var offset):
                ptr = indexedPtr;
                index = offset;
                return true;

            default:
                return false;
        }
    }

    private static bool TryExtractPointerIndex(Math2Expr add, out Variable ptr, out int index)
    {
        ptr = null!;
        index = 0;

        if (add.First is Variable first && !IsSegmentBase(first) && add.Second is ConstExpr offset)
        {
            ptr = first;
            index = offset.Value;
            return true;
        }

        if (add.Second is Variable second && !IsSegmentBase(second) && add.First is ConstExpr offset2)
        {
            ptr = second;
            index = offset2.Value;
            return true;
        }

        return false;
    }

    private static bool IsSegmentBase(Variable variable) =>
        variable.Name is "_psp";
}

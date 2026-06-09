using UltraDecompiler.Decompilation;

namespace UltraDecompiler.CodeGeneration;

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
        lvalue = string.Empty;

        // Near-указатель в small-модели: сегмент (DS/_psp) + смещение в регистре-указателе.
        if (store.Segment is null)
        {
            return false;
        }

        switch (store.Address)
        {
            case Variable ptr when !IsSegmentBase(ptr):
                lvalue = $"{ptr}[0]";
                return true;

            case Math2Expr { Operation: Math2Operation.Add } add
                when TryExtractPointerIndex(add, out var basePtr, out var index):
                lvalue = $"{basePtr}[{index}]";
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

using UltraDecompiler.Decompilation;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Форматирует разыменование far-указателя (<c>*ptr</c>, <c>ptr[i]</c>) вместо <c>seg:[off]</c>.
/// </summary>
public static class FarPointerFormatter
{
    /// <summary>
    /// Пытается представить запись через far-указатель как <c>*var</c> или <c>var[index]</c>.
    /// </summary>
    public static bool TryFormatStore(StoreOperation store, out string lvalue)
    {
        lvalue = string.Empty;

        if (!TryGetFarPointerBase(store, out var farPtr, out var index))
        {
            return false;
        }

        lvalue = index == 0
            ? $"*{farPtr}"
            : $"{farPtr}[{index}]";
        return true;
    }

    /// <summary>
    /// Пытается представить чтение через far-указатель как <c>*var</c> или <c>var[index]</c>.
    /// </summary>
    public static bool TryFormatLoad(MemExpr mem, out string formatted)
    {
        formatted = string.Empty;

        if (!TryGetFarPointerBase(mem, out var farPtr, out var index))
        {
            return false;
        }

        formatted = index == 0
            ? $"*{farPtr}"
            : $"{farPtr}[{index}]";
        return true;
    }

    private static bool TryGetFarPointerBase(StoreOperation store, out Variable farPtr, out int index)
    {
        farPtr = null!;
        index = 0;

        if (store.Segment is not Variable segmentVar)
        {
            return false;
        }

        switch (store.Address)
        {
            case Variable offsetVar when IsFarPointerPair(offsetVar, segmentVar):
                farPtr = offsetVar;
                return true;

            case Math2Expr { Operation: Math2Operation.Add } add
                when TryExtractPointerIndex(add, out var indexedPtr, out var offset)
                && IsFarPointerPair(indexedPtr, segmentVar):
                farPtr = indexedPtr;
                index = offset;
                return true;

            default:
                return false;
        }
    }

    private static bool TryGetFarPointerBase(MemExpr mem, out Variable farPtr, out int index)
    {
        farPtr = null!;
        index = 0;

        if (mem.Segment is not Variable segmentVar)
        {
            return false;
        }

        switch (mem.Address)
        {
            case Variable offsetVar when IsFarPointerPair(offsetVar, segmentVar):
                farPtr = offsetVar;
                return true;

            case Math2Expr { Operation: Math2Operation.Add } add
                when TryExtractPointerIndex(add, out var indexedPtr, out var offset)
                && IsFarPointerPair(indexedPtr, segmentVar):
                farPtr = indexedPtr;
                index = offset;
                return true;

            default:
                return false;
        }
    }

    private static bool IsFarPointerPair(Variable offsetVar, Variable segmentVar) =>
        offsetVar.Type?.IsCharFarPtr == true
        && ReferenceEquals(offsetVar.FarPointerSegmentVariable, segmentVar);

    private static bool TryExtractPointerIndex(Math2Expr add, out Variable ptr, out int index)
    {
        ptr = null!;
        index = 0;

        if (add.First is Variable first && add.Second is ConstExpr offset)
        {
            ptr = first;
            index = offset.Value;
            return true;
        }

        if (add.Second is Variable second && add.First is ConstExpr offset2)
        {
            ptr = second;
            index = offset2.Value;
            return true;
        }

        return false;
    }
}

namespace UltraDecompiler.CodeGeneration.Rendering;

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

        lvalue = FormatIndexed(farPtr, index);
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

        formatted = FormatIndexed(farPtr, index);
        return true;
    }

    private static string FormatIndexed(Variable farPtr, Expr? index) =>
        index switch
        {
            null or ConstExpr { Value: 0 } => $"*{farPtr}",
            ConstExpr constant => $"{farPtr}[{constant.Value}]",
            _ => $"{farPtr}[{index}]",
        };

    private static bool TryGetFarPointerBase(StoreOperation store, out Variable farPtr, out Expr? index)
    {
        farPtr = null!;
        index = null;

        if (store.Segment is not VariableExpr { Var: var segmentVar })
        {
            return false;
        }

        switch (store.Address)
        {
            case VariableExpr { Var: var offsetVar } when IsFarPointerPair(offsetVar, segmentVar):
                farPtr = offsetVar;
                return true;

            case Math2Expr { Operation: Math2Operation.Add } add
                when TryExtractPointerIndex(add, out var indexedPtr, out index)
                && IsFarPointerPair(indexedPtr, segmentVar):
                farPtr = indexedPtr;
                return true;

            default:
                return false;
        }
    }

    private static bool TryGetFarPointerBase(MemExpr mem, out Variable farPtr, out Expr? index)
    {
        farPtr = null!;
        index = null;

        if (mem.Segment is not VariableExpr { Var: var segmentVar })
        {
            return false;
        }

        switch (mem.Address)
        {
            case VariableExpr { Var: var offsetVar } when IsFarPointerPair(offsetVar, segmentVar):
                farPtr = offsetVar;
                return true;

            case Math2Expr { Operation: Math2Operation.Add } add
                when TryExtractPointerIndex(add, out var indexedPtr, out index)
                && IsFarPointerPair(indexedPtr, segmentVar):
                farPtr = indexedPtr;
                return true;

            default:
                return false;
        }
    }

    private static bool IsFarPointerPair(Variable offsetVar, Variable segmentVar) =>
        offsetVar.Type?.IsCharFarPtr == true
        && ReferenceEquals(offsetVar.FarPointerSegmentVariable, segmentVar);

    private static bool TryExtractPointerIndex(Math2Expr add, out Variable ptr, out Expr? index)
    {
        ptr = null!;
        index = null;

        if (add.First is VariableExpr { Var: var first } && add.Second is ConstExpr offset)
        {
            ptr = first;
            index = offset.Value == 0 ? null : offset;
            return true;
        }

        if (add.Second is VariableExpr { Var: var second } && add.First is ConstExpr offset2)
        {
            ptr = second;
            index = offset2.Value == 0 ? null : offset2;
            return true;
        }

        if (add.First is VariableExpr { Var: var firstVar } && add.Second is not VariableExpr { Var: { IsTemp: false } })
        {
            ptr = firstVar;
            index = add.Second;
            return true;
        }

        if (add.Second is VariableExpr { Var: var secondVar } && add.First is not VariableExpr { Var: { IsTemp: false } })
        {
            ptr = secondVar;
            index = add.First;
            return true;
        }

        return false;
    }
}

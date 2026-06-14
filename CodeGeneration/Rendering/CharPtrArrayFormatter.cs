namespace UltraDecompiler.CodeGeneration.Rendering;

/// <summary>
/// Форматирование обращений к массивам near-указателей (<c>argv[i]</c>, <c>envp[i]</c>, <c>s[n]</c>).
/// </summary>
public static class CharPtrArrayFormatter
{
    /// <summary>
    /// Пытается переписать загрузку из массива указателей/строк в идиоматичное выражение для кодогенерации.
    /// </summary>
    public static bool TryRewriteLoad(MemExpr mem, out Expr rewritten)
    {
        rewritten = mem;

        if (!PointerDerefFormatter.IsNearDataSegment(mem.Segment))
        {
            return false;
        }

        if (TryFormatCharPtrArrayElement(mem, out var formatted, out var array, out var index)
            || TryFormatCharPtrElement(mem, out formatted, out array, out index))
        {
            rewritten = new SyntheticLoadExpr(formatted, array, index);
            return true;
        }

        return false;
    }

    /// <summary>Форматирует загрузку из массива <c>char*</c> (<c>argv[i]</c>).</summary>
    public static bool TryFormatCharPtrArrayElement(
        MemExpr mem,
        out string formatted,
        out Variable? array,
        out Variable? index)
    {
        formatted = string.Empty;
        array = null;
        index = null;

        if (mem.Address is Variable arrayVar && arrayVar.Type?.IsCharPtrPtr == true)
        {
            array = arrayVar;
            formatted = $"{arrayVar}[0]";
            return true;
        }

        if (mem.Address is not Math2Expr { Operation: Math2Operation.Add } add)
        {
            return false;
        }

        if (!TryParseScaledWordIndex(add, out var baseVar, out var indexVar, out var indexExpr))
        {
            return false;
        }

        if (baseVar.Type?.IsCharPtrPtr != true)
        {
            return false;
        }

        array = baseVar;
        index = indexVar;
        formatted = $"{baseVar}[{indexExpr}]";
        return true;
    }

    /// <summary>Форматирует загрузку по <c>char*</c> с константным смещением (<c>s[n]</c>).</summary>
    public static bool TryFormatCharPtrElement(
        MemExpr mem,
        out string formatted,
        out Variable? array,
        out Variable? index)
    {
        formatted = string.Empty;
        array = null;
        index = null;

        if (mem.Address is Variable ptr && ptr.Type?.IsCharPtr == true)
        {
            array = ptr;
            formatted = $"*{ptr}";
            return true;
        }

        if (mem.Address is not Math2Expr { Operation: Math2Operation.Add } add)
        {
            return false;
        }

        if (add.First is Variable first && !IsSegmentBase(first) && add.Second is ConstExpr offset)
        {
            return FormatCharPtrIndexed(first, offset.Value, out formatted, out array);
        }

        if (add.Second is Variable second && !IsSegmentBase(second) && add.First is ConstExpr offset2)
        {
            return FormatCharPtrIndexed(second, offset2.Value, out formatted, out array);
        }

        return false;
    }

    private static bool FormatCharPtrIndexed(
        Variable ptr,
        int byteOffset,
        out string formatted,
        out Variable? array)
    {
        formatted = string.Empty;

        array = null;
        if (ptr.Type?.IsCharPtr != true)
        {
            return false;
        }

        array = ptr;
        formatted = byteOffset == 0 ? $"*{ptr}" : $"{ptr}[{byteOffset}]";
        return true;
    }

    private static bool TryParseScaledWordIndex(
        Math2Expr add,
        out Variable baseVar,
        out Variable? indexVar,
        out string indexExpr)
    {
        baseVar = null!;
        indexVar = null;
        indexExpr = string.Empty;

        if (TryParseScaledWordIndex(add.First, add.Second, out baseVar, out indexVar, out indexExpr))
        {
            return true;
        }

        return TryParseScaledWordIndex(add.Second, add.First, out baseVar, out indexVar, out indexExpr);
    }

    private static bool TryParseScaledWordIndex(
        Expr left,
        Expr right,
        out Variable baseVar,
        out Variable? indexVar,
        out string indexExpr)
    {
        baseVar = null!;
        indexVar = null;
        indexExpr = string.Empty;

        if (left is Variable variable && !IsSegmentBase(variable))
        {
            if (right is ConstExpr { Value: 0 })
            {
                baseVar = variable;
                indexExpr = "0";
                return true;
            }

            if (TryGetWordIndex(right, out indexVar, out indexExpr))
            {
                baseVar = variable;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetWordIndex(Expr expr, out Variable? indexVar, out string indexExpr)
    {
        indexVar = null;
        indexExpr = string.Empty;

        switch (expr)
        {
            case Variable variable:
                indexVar = variable;
                indexExpr = variable.ToString();
                return true;

            case ConstExpr { Value: var value }:
                if (value % 2 != 0)
                {
                    return false;
                }

                indexExpr = (value / 2).ToString();
                return true;

            case Math2Expr { Operation: Math2Operation.Shl, First: Variable index, Second: ConstExpr { Value: 1 } }:
                indexVar = index;
                indexExpr = index.ToString();
                return true;

            case Math2Expr { Operation: Math2Operation.Mul, First: Variable mulIndex, Second: ConstExpr { Value: 2 } }:
                indexVar = mulIndex;
                indexExpr = mulIndex.ToString();
                return true;

            case Math2Expr { Operation: Math2Operation.Mul, First: ConstExpr { Value: 2 }, Second: Variable mulIndex2 }:
                indexVar = mulIndex2;
                indexExpr = mulIndex2.ToString();
                return true;

            default:
                return false;
        }
    }

    private static bool IsSegmentBase(Variable variable) =>
        variable.Name is "_psp";

    /// <summary>Проверяет, что выражение — доступ к элементу <c>argv[]</c> / <c>envp[]</c>.</summary>
    public static bool IsArgvEnvpElementAccess(Expr expr) =>
        expr is SyntheticLoadExpr synthetic
        && synthetic.Array?.Name is "argv" or "envp";
}

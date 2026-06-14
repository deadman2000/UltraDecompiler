namespace UltraDecompiler.PostProcessing.Normalization;

/// <summary>
/// QuickC /Ox: <c>inc/dec [v]</c> перед <c>v = v ± 1</c> (из mov reg; inc reg; mov [v],reg)
/// переупорядочивает в стиль исходника: сначала присваивание, затем ++/--.
/// </summary>
public static class IncDecSequenceNormalizer
{
    /// <summary>
    /// Меняет местами соседние inc/dec и set v=v±1 для одной переменной.
    /// </summary>
    public static IReadOnlyList<Operation> Normalize(IReadOnlyList<Operation> operations) =>
        NormalizeList(operations.ToList());

    private static List<Operation> NormalizeList(List<Operation> operations)
    {
        for (var i = 0; i + 1 < operations.Count; i++)
        {
            if (operations[i] is IncOperation inc
                && operations[i + 1] is SetOperation { Dst: Variable dst, Src: Math2Expr math }
                && ReferenceEquals(inc.Target, dst)
                && math.First is Variable first
                && ReferenceEquals(first, dst)
                && math.Second is ConstExpr { Value: 1 }
                && math.Operation is Math2Operation.Add)
            {
                (operations[i], operations[i + 1]) = (operations[i + 1], operations[i]);
                i++;
                continue;
            }

            if (operations[i] is DecOperation dec
                && operations[i + 1] is SetOperation { Dst: Variable dst2, Src: Math2Expr math2 }
                && ReferenceEquals(dec.Target, dst2)
                && math2.First is Variable first2
                && ReferenceEquals(first2, dst2)
                && math2.Second is ConstExpr { Value: 1 }
                && math2.Operation is Math2Operation.Sub)
            {
                (operations[i], operations[i + 1]) = (operations[i + 1], operations[i]);
                i++;
            }
        }

        return operations;
    }
}

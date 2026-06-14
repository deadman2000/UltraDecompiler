namespace UltraDecompiler.Ir.Builder.Patterns;

/// <summary>
/// QuickC /Ox: <c>mov ax,[v]; inc/dec ax; mov [v],ax; inc/dec [v]</c> — placeholder;
/// переупорядочивание inc/dec выполняется в <c>IncDecSequenceNormalizer</c> (PostProcessing).
/// </summary>
public static class RegisterIncDecPattern
{
    /// <summary>Зарезервировано для будущих peephole на уровне блока.</summary>
    public static void Apply(ExprBlock block)
    {
    }
}

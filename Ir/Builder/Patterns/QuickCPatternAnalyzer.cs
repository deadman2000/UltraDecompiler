namespace UltraDecompiler.Ir.Builder.Patterns;

/// <summary>
/// Применяет QuickC-специфичные паттерны к IR блока после per-instruction handlers.
/// </summary>
internal static class QuickCPatternAnalyzer
{
    /// <summary>
    /// Сканирует инструкции блока и корректирует IR (push-аргументы, inc/dec и т.д.).
    /// </summary>
    public static void Apply(ExprBlock block)
    {
        StackLocalPushArgPattern.Apply(block);
        RegisterIncDecPattern.Apply(block);
    }
}

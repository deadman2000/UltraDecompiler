using UltraDecompiler.Common;

namespace UltraDecompiler.Ir.Builder.Loops;

/// <summary>
/// Фабрика анализаторов циклов для разных профилей компиляции QuickC.
/// </summary>
public static class LoopAnalyzerFactory
{
    /// <summary>
    /// Создаёт анализатор циклов для указанного уровня оптимизации.
    /// </summary>
    public static ILoopAnalyzer Create(OptimizationLevel optimization) => optimization switch
    {
        OptimizationLevel.Disabled => new QuickCUnoptLoopAnalyzer(),
        OptimizationLevel.Enabled or
        OptimizationLevel.EnableLoop or
        OptimizationLevel.EnabledFull => new QuickCOptLoopAnalyzer(),
        _ => new QuickCUnoptLoopAnalyzer()
    };
}

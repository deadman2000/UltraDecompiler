using UltraDecompiler.Compilation;
using UltraDecompiler.PostProcessing.Abstractions;
using UltraDecompiler.PostProcessing.Profiles.QuickC;

namespace UltraDecompiler.PostProcessing.Profiles;

/// <summary>Выбор профиля декомпиляции по уровню оптимизации QuickC.</summary>
public static class DecompilationProfileRegistry
{
    /// <summary>
    /// Возвращает профиль для заданного уровня оптимизации.
    /// Для оптимизированных уровней возвращает QuickCOptimizedProfile с точным уровнем (для правильного CompilerOptions и Makefile).
    /// </summary>
    public static IDecompilationProfile GetProfile(OptimizationLevel level) =>
        level switch
        {
            OptimizationLevel.Disabled => QuickCUnoptimizedProfile.Instance,
            _ => QuickCOptimizedFullProfile.Instance,
        };
}

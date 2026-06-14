using UltraDecompiler.Compilation;
using UltraDecompiler.PostProcessing.Abstractions;
using UltraDecompiler.PostProcessing.Profiles.QuickC;

namespace UltraDecompiler.PostProcessing.Profiles;

/// <summary>Выбор профиля декомпиляции по уровню оптимизации QuickC.</summary>
public static class DecompilationProfileRegistry
{
    /// <summary>
    /// Возвращает профиль для заданного уровня оптимизации.
    /// Пока для всех уровней кроме <see cref="OptimizationLevel.Disabled"/> — заглушка оптимизированного профиля.
    /// </summary>
    public static IDecompilationProfile GetProfile(OptimizationLevel level) =>
        level switch
        {
            OptimizationLevel.Disabled => QuickCUnoptimizedProfile.Instance,
            _ => QuickCOptimizedProfile.Instance,
        };
}

using UltraDecompiler.Compilation;

namespace UltraDecompiler.PostProcessing.Abstractions;

/// <summary>
/// Профиль декомпиляции: IR-construction hooks и упорядоченные post-process pass-ы.
/// </summary>
public interface IDecompilationProfile
{
    OptimizationLevel OptimizationLevel { get; }

    IReadOnlyList<IPostProcessPass> GetProcedurePasses();
}
